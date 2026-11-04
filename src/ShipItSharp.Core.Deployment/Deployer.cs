#region copyright
// /*
//     ShipItSharp Deployment Coordinator. Provides extra tooling to help
//     deploy software through Octopus Deploy.
// 
//     Copyright (C) 2022  Steven Davies
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
// */
#endregion


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Deployment.Models.Interfaces;
using ShipItSharp.Core.Deployment.Resources;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;
using Environment = System.Environment;
using TaskStatus = ShipItSharp.Core.Deployment.Models.TaskStatus;

namespace ShipItSharp.Core.Deployment
{
    public class Deployer : IDeployer
    {
        private readonly IConfiguration _configuration;
        private readonly IOctopusHelper _helper;
        private readonly ILanguageProvider _languageProvider;
        private readonly IDeploymentQueueInteraction _queueInteraction;

        public Deployer(IOctopusHelper helper, IConfiguration configuration, ILanguageProvider languageProvider, IDeploymentQueueInteraction queueInteraction)
        {
            _helper = helper;
            _configuration = configuration;
            _languageProvider = languageProvider;
            _queueInteraction = queueInteraction;
        }

        public async Task<DeploymentCheckResult> CheckDeployment(EnvironmentDeployment deployment)
        {
            foreach (var project in deployment.ProjectDeployments)
            {
                var lifeCyle = await _helper.LifeCycles.GetLifeCycle(project.LifeCycleId);
                if (lifeCyle.Phases.Any())
                {
                    var safe = false;
                    var firstPhase = lifeCyle.Phases[0];
                    
                    // Check if target environment is directly allowed in first phase
                    if (firstPhase.OptionalDeploymentTargetEnvironmentIds.Contains(deployment.EnvironmentId) ||
                        firstPhase.AutomaticDeploymentTargetEnvironmentIds.Contains(deployment.EnvironmentId))
                    {
                        safe = true;
                    }
                    
                    // If first phase has no specific environments (allows any environment), it's safe
                    if (!safe && !firstPhase.OptionalDeploymentTargetEnvironmentIds.Any() && 
                        !firstPhase.AutomaticDeploymentTargetEnvironmentIds.Any())
                    {
                        safe = true;
                    }
                    
                    if (!safe)
                    {
                        var phaseCheck = true;
                        var deployments = await _helper.Deployments.GetDeployments(project.ReleaseId);
                        var previousEnvs = deployments.Select(d => d.EnvironmentId).ToList();
                        
                        // Find which phase the target environment belongs to
                        Phase targetPhase = null;
                        foreach (var phase in lifeCyle.Phases)
                        {
                            var allPhaseEnvs = phase.OptionalDeploymentTargetEnvironmentIds
                                .Concat(phase.AutomaticDeploymentTargetEnvironmentIds).ToList();
                            
                            // If phase has no specific environments, it accepts any environment
                            if (!allPhaseEnvs.Any() || allPhaseEnvs.Contains(deployment.EnvironmentId))
                            {
                                targetPhase = phase;
                                break;
                            }
                        }
                        
                        if (targetPhase == null)
                        {
                            phaseCheck = false;
                        }
                        else
                        {
                            // Check all phases before the target phase
                            var targetPhaseIndex = lifeCyle.Phases.IndexOf(targetPhase);
                            for (int i = 0; i < targetPhaseIndex; i++)
                            {
                                var phase = lifeCyle.Phases[i];
                                if (!phase.Optional)
                                {
                                    var phaseEnvs = phase.OptionalDeploymentTargetEnvironmentIds
                                        .Concat(phase.AutomaticDeploymentTargetEnvironmentIds).ToList();
                                    
                                    // If phase has no specific environments, any previous deployment satisfies it
                                    if (!phaseEnvs.Any())
                                    {
                                        if (phase.MinimumEnvironmentsBeforePromotion > 0 && 
                                            previousEnvs.Count < phase.MinimumEnvironmentsBeforePromotion)
                                        {
                                            phaseCheck = false;
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        var deploymentsInPhase = previousEnvs.Intersect(phaseEnvs).Count();
                                        if (phase.MinimumEnvironmentsBeforePromotion == 0)
                                        {
                                            // At least one deployment must be in this phase
                                            if (deploymentsInPhase == 0)
                                            {
                                                phaseCheck = false;
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            if (deploymentsInPhase < phase.MinimumEnvironmentsBeforePromotion)
                                            {
                                                phaseCheck = false;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        safe = phaseCheck;
                    }
                    
                    if (!safe)
                    {
                        return new DeploymentCheckResult
                        {
                            Success = false,
                            ErrorMessage = DeploymentStrings.FailedValidation.Replace("{{projectname}}", project.ProjectName).Replace("{{environmentname}}", deployment.EnvironmentName)
                        };
                    }
                }
            }
            return new DeploymentCheckResult { Success = true };
        }

        public async Task StartJob(IOctoJob job, IUiLogger uiLogger, bool suppressMessages = false)
        {
            if (job is EnvironmentDeployment)
            {
                await ProcessEnvironmentDeployment((EnvironmentDeployment) job, suppressMessages, uiLogger);
            }
        }

        public void FillRequiredVariables(List<ProjectDeployment> projects, Func<string, string> userPrompt, bool runningInteractively)
        {
            if (!runningInteractively)
            {
                return;
            }
            foreach (var project in projects)
            {
                if (project.RequiredVariables != null)
                {
                    foreach (var requirement in project.RequiredVariables)
                    {
                        do
                        {
                            var prompt = string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "VariablePrompt"), requirement.Name, project.ProjectName);
                            if (!string.IsNullOrEmpty(requirement.ExtraOptions))
                            {
                                prompt = prompt + string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "VariablePromptAllowedValues"), requirement.ExtraOptions);
                            }
                            requirement.Value = userPrompt(prompt);
                        } while (string.IsNullOrEmpty(requirement.Value));
                    }

                }
            }
        }

        private async Task ProcessEnvironmentDeployment(EnvironmentDeployment deployment, bool suppressMessages,
            IUiLogger uiLogger)
        {
            var stopwatch = Stopwatch.StartNew();
            var targetName = GetDeploymentTargetName(deployment);
            uiLogger.WriteLine(string.Format(UiString("ShipItSharpDeploymentToTarget"), targetName));
            var failedProjects = new Dictionary<ProjectDeployment, TaskDetails>();

            var projectRegister = new Dictionary<string, ProjectDeployment>();

            foreach (var project in deployment.ProjectDeployments)
            {
                Release result;
                if (string.IsNullOrEmpty(project.ReleaseId))
                {
                    WriteDeploymentStatus(uiLogger, "StatusRun", string.Format(UiString("CreatingReleaseForProject"), project.ProjectName));
                    result = await _helper.Releases.CreateRelease(project, deployment.FallbackToDefaultChannel);
                    WriteDeploymentStatus(uiLogger, "StatusDone", string.Format(UiString("CreatedReleaseForProject"), project.ProjectName, result.Version));
                }
                else
                {
                    WriteDeploymentStatus(uiLogger, "StatusRun", string.Format(UiString("FetchingReleaseForProject"), project.ProjectName));
                    result = await _helper.Releases.GetRelease(project.ReleaseId);
                    WriteDeploymentStatus(uiLogger, "StatusDone", string.Format(UiString("FetchedReleaseForProject"), project.ProjectName, result.Version));
                }

                WriteDeploymentStatus(uiLogger, "StatusRun", string.Format(UiString("CreatingOctopusTaskForProject"), project.ProjectName, targetName));
                var deployResult = await _helper.Deployments.CreateDeploymentTask(project, deployment.EnvironmentId, result.Id, deployment.Prioritise, deployment.MachineId);
                WriteDeploymentStatus(uiLogger, "StatusDone", string.Format(UiString("CreatedOctopusTask"), deployResult.TaskId));

                if (deployment.DeployAsync)
                {
                    projectRegister.Add(deployResult.TaskId, project);
                }
                else
                {
                    var taskDeets = await _helper.Deployments.GetTaskDetails(deployResult.TaskId);
                    if (taskDeets.State == TaskStatus.Failed)
                    {
                        WriteDeploymentStatus(uiLogger, "StatusFailed", string.Format(UiString("ProjectFailedToDeploy"), project.ProjectName));
                        failedProjects.Add(project, taskDeets);
                    }
                    else
                    {
                        WriteDeploymentStatus(uiLogger, "StatusDone", string.Format(UiString("ProjectDeployedToEnvironment"), project.ProjectName, targetName));
                    }
                    uiLogger.WriteLine(UiString("RawLog") + Environment.NewLine +
                                       await _helper.Deployments.GetTaskRawLog(taskDeets.TaskId));
                }
            }

            var unconfirmed = deployment.DeployAsync
                ? await DeployAsync(uiLogger, failedProjects, projectRegister, targetName)
                : 0;

            if (failedProjects.Any())
            {
                uiLogger.WriteLine(UiString("FailedProjects"));
                foreach (var failure in failedProjects)
                {
                    var link = string.Empty;
                    if (failure.Value.Links != null)
                    {
                        if (failure.Value.Links.ContainsKey("Web"))
                        {
                            link = _configuration.OctopusUrl + failure.Value.Links["Web"];
                        }
                    }
                    uiLogger.WriteLine(string.Format(UiString("FailedProjectLink"), failure.Key.ProjectName, link));
                }
            }
            if (!suppressMessages)
            {
                var shipped = deployment.ProjectDeployments.Count - failedProjects.Count - unconfirmed;
                WriteDeploymentSummary(uiLogger, shipped, failedProjects.Count, unconfirmed, stopwatch.Elapsed);
            }
        }

        private async Task<int> DeployAsync(IUiLogger uiLogger, Dictionary<ProjectDeployment, TaskDetails> failedProjects, Dictionary<string, ProjectDeployment> pendingProjects, string targetName)
        {
            var totalCount = pendingProjects.Count;
            var unconfirmed = 0;
            int? lastReportedQueuePosition = null;
            var queueJumpAttempted = false;
            _queueInteraction.Reset();
            using var queueScanCancellation = new CancellationTokenSource();
            Task<(List<TaskStub> Tasks, Exception Error)> queueScan = null;

            try
            {
                while (true)
                {
                    var taskIds = pendingProjects.Keys.ToList();
                    var statuses = await Task.WhenAll(taskIds.Select(id => _helper.Deployments.GetTaskStatus(id)));
                    var queuedIds = new HashSet<string>();
                    for (var index = 0; index < taskIds.Count; index++)
                    {
                        var taskId = taskIds[index];
                        var found = statuses[index];
                        var project = pendingProjects[taskId];
                        if (found == null)
                        {
                            uiLogger.CleanCurrentLine();
                            WriteDeploymentStatus(uiLogger, "StatusFailed", string.Format(UiString("OctopusTaskNotFound"), taskId));
                            failedProjects.Add(project, new TaskDetails { TaskId = taskId });
                            pendingProjects.Remove(taskId);
                        }
                        else if (found.State == TaskStatus.Unconfirmed)
                        {
                            uiLogger.CleanCurrentLine();
                            WriteDeploymentStatus(uiLogger, "StatusCheck", string.Format(UiString("TaskStatusRateLimited"), project.ProjectName, taskId));
                            unconfirmed++;
                            pendingProjects.Remove(taskId);
                        }
                        else if (found.State == TaskStatus.Done)
                        {
                            uiLogger.CleanCurrentLine();
                            WriteDeploymentStatus(uiLogger, "StatusDone", string.Format(UiString("ProjectDeployedToEnvironment"), project.ProjectName, targetName));
                            pendingProjects.Remove(taskId);
                        }
                        else if (found.State == TaskStatus.Failed)
                        {
                            uiLogger.CleanCurrentLine();
                            WriteDeploymentStatus(uiLogger, "StatusFailed", string.Format(UiString("ProjectFailedWithError"), project.ProjectName, found.ErrorMessage));
                            failedProjects.Add(project, new TaskDetails { TaskId = taskId, Links = found.Links });
                            pendingProjects.Remove(taskId);
                        }
                        else if (found.State == TaskStatus.Queued)
                        {
                            queuedIds.Add(taskId);
                        }
                    }

                    if (pendingProjects.Count == 0)
                    {
                        uiLogger.WriteProgress(totalCount, totalCount, $"Deploying the requested projects ({totalCount} of {totalCount} done)...");
                        break;
                    }

                    if (queueScan == null && queuedIds.Count > 0)
                    {
                        queueScan = GetQueuedDeploymentTasks(queueScanCancellation.Token);
                    }

                    (List<TaskStub> Tasks, Exception Error) scanResult = (null, null);
                    if (queueScan != null && queueScan.IsCompleted)
                    {
                        scanResult = await queueScan;
                        queueScan = null;
                    }
                    if (scanResult.Error != null)
                    {
                        uiLogger.CleanCurrentLine();
                        WriteDeploymentStatus(uiLogger, "StatusFailed", string.Format(UiString("QueueLookupFailed"), scanResult.Error.Message));
                    }
                    var queuedTasks = (scanResult.Tasks ?? new List<TaskStub>())
                        .Where(task => task.State == TaskStatus.Queued)
                        .OrderBy(task => task.QueueTime ?? DateTimeOffset.MaxValue)
                        .ToList();
                    var queuedJobTaskIds = queuedTasks.Select(task => task.TaskId)
                        .Where(queuedIds.Contains)
                        .Distinct()
                        .ToList();
                    var leadingQueuePosition = queuedJobTaskIds.Count == 0
                        ? 0
                        : queuedTasks.FindIndex(task => task.TaskId == queuedJobTaskIds[0]) + 1;
                    if (leadingQueuePosition > 1 && leadingQueuePosition != lastReportedQueuePosition)
                    {
                        uiLogger.CleanCurrentLine();
                        WriteDeploymentStatus(uiLogger, "StatusQueued", string.Format(UiString("LeadingDeploymentQueuePosition"), leadingQueuePosition));
                        lastReportedQueuePosition = leadingQueuePosition;
                    }

                    var shouldReadQueueJump = queuedJobTaskIds.Count > 0 && (leadingQueuePosition > 1 || queueJumpAttempted);
                    var queueJumpRequested = shouldReadQueueJump && _queueInteraction.QueueJumpRequested();
                    if (!queueJumpAttempted && leadingQueuePosition > 1 && queueJumpRequested)
                    {
                        uiLogger.CleanCurrentLine();
                        if (_queueInteraction.ConfirmQueueJump(string.Format(UiString("ConfirmQueueJump"), queuedJobTaskIds.Count)))
                        {
                            queueJumpAttempted = true;
                            var prioritisedTaskCount = 0;
                            foreach (var taskId in queuedJobTaskIds.AsEnumerable().Reverse())
                            {
                                try
                                {
                                    await _helper.Deployments.PrioritiseTask(taskId);
                                    prioritisedTaskCount++;
                                }
                                catch (Exception exception)
                                {
                                    WriteDeploymentStatus(uiLogger, "StatusFailed", string.Format(UiString("QueueJumpFailed"), taskId, exception.Message));
                                }
                            }
                            if (prioritisedTaskCount > 0)
                            {
                                WriteDeploymentStatus(uiLogger, "StatusDone", string.Format(UiString("QueueJumpRequested"), prioritisedTaskCount));
                            }
                        }
                    }

                    var completedCount = totalCount - pendingProjects.Count;
                    uiLogger.WriteProgress(completedCount, totalCount, $"Deploying the requested projects ({completedCount} of {totalCount} done)...");
                    await Task.Delay(3000);
                }
            }
            finally
            {
                queueScanCancellation.Cancel();
                uiLogger.StopAnimation();
                uiLogger.CleanCurrentLine();
            }
            return unconfirmed;
        }

        private async Task<(List<TaskStub> Tasks, Exception Error)> GetQueuedDeploymentTasks(CancellationToken cancellationToken)
        {
            const int pageSize = 100;
            var tasks = new List<TaskStub>();
            var skip = 0;
            IReadOnlyCollection<TaskStub> page;
            try
            {
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    page = (await _helper.Deployments.GetQueuedDeploymentTasks(skip, pageSize, cancellationToken)).ToList();
                    tasks.AddRange(page);
                    skip += pageSize;
                } while (page.Count == pageSize);

                return (tasks, null);
            }
            catch (Exception exception)
            {
                return (null, exception);
            }
        }

        private static string GetDeploymentTargetName(EnvironmentDeployment deployment)
        {
            if (string.IsNullOrWhiteSpace(deployment.MachineName))
            {
                return deployment.EnvironmentName;
            }

            return $"{deployment.EnvironmentName} on machine {deployment.MachineName}";
        }

        private void WriteDeploymentSummary(IUiLogger uiLogger, int completed, int failed, int unconfirmed, TimeSpan elapsed)
        {
            uiLogger.WriteLine(UiString("DeploymentSummary"));
            WriteDeploymentStatus(uiLogger, "StatusDone", string.Format(UiString("DeploymentSummaryCompleted"), completed));
            WriteDeploymentStatus(uiLogger, failed == 0 ? "StatusDone" : "StatusFailed", string.Format(UiString("DeploymentSummaryFailed"), failed));
            if (unconfirmed > 0)
            {
                WriteDeploymentStatus(uiLogger, "StatusCheck", string.Format(UiString("DeploymentSummaryUnconfirmed"), unconfirmed));
            }
            WriteDeploymentStatus(uiLogger, "StatusRun", string.Format(UiString("DeploymentSummaryTotal"), completed + failed + unconfirmed));
            WriteDeploymentStatus(uiLogger, "StatusRun", string.Format(UiString("DeploymentElapsedTime"), elapsed.ToString(@"hh\:mm\:ss")));
        }

        private async Task<TaskDetails> StartDeployment(IUiLogger uiLogger, TaskDetails taskDeets, bool doWait)
        {
            do
            {
                WriteStatus(uiLogger, taskDeets);
                if (doWait)
                {
                    await Task.Delay(1000);
                }
                taskDeets = await _helper.Deployments.GetTaskDetails(taskDeets.TaskId);
            } while (doWait && (taskDeets.State == TaskStatus.InProgress || taskDeets.State == TaskStatus.Queued));
            return taskDeets;
        }

        private void WriteStatus(IUiLogger uiLogger, TaskDetails taskDeets)
        {
            if (taskDeets.State != TaskStatus.Queued)
            {
                if (taskDeets.PercentageComplete < 100)
                {
                    WriteDeploymentStatus(uiLogger, "StatusRun", string.Format(UiString("TaskStatusProgress"),
                        taskDeets.State,
                        taskDeets.PercentageComplete,
                        taskDeets.TimeLeft));
                }
            }
            else
            {
                WriteDeploymentStatus(uiLogger, "StatusRun", UiString("TaskQueuedWaiting"));
            }
        }

        private void WriteDeploymentStatus(IUiLogger uiLogger, string statusKey, string message)
        {
            var status = UiString(statusKey);
            uiLogger.WriteLine($"  {status,-6} {message}");
        }

        private string UiString(string key)
        {
            return _languageProvider.GetString(LanguageSection.UiStrings, key);
        }
    }
}
