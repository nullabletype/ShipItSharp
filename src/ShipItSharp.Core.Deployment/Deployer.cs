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
using System.Linq;
using System.Threading.Tasks;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Deployment.Models.Interfaces;
using ShipItSharp.Core.Deployment.Resources;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Logging;
using ShipItSharp.Core.Logging.Interfaces;
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
        private readonly ILogger _log;

        public Deployer(IOctopusHelper helper, IConfiguration configuration, ILanguageProvider languageProvider)
        {
            _helper = helper;
            _configuration = configuration;
            _languageProvider = languageProvider;
            _log = LoggingProvider.GetLogger<Deployer>();
        }

        public async Task<DeploymentCheckResult> CheckDeployment(EnvironmentDeployment deployment)
        {
            foreach (var project in deployment.ProjectDeployments)
            {
                _log.Info($"Checking lifecycle for project {project.ProjectName}");
                var lifeCyle = await _helper.LifeCycles.GetLifeCycle(project.LifeCycleId);
                if (lifeCyle.Phases.Any())
                {
                    var safe = false;
                    if (lifeCyle.Phases[0].OptionalDeploymentTargetEnvironmentIds.Any())
                    {
                        if (lifeCyle.Phases[0].OptionalDeploymentTargetEnvironmentIds.Contains(deployment.EnvironmentId))
                        {
                            safe = true;
                        }
                    }
                    if (!safe && lifeCyle.Phases[0].AutomaticDeploymentTargetEnvironmentIds.Any())
                    {
                        if (lifeCyle.Phases[0].AutomaticDeploymentTargetEnvironmentIds.Contains(deployment.EnvironmentId))
                        {
                            safe = true;
                        }
                    }
                    if (!safe)
                    {
                        var phaseCheck = true;
                        var deployments = await _helper.Deployments.GetDeployments(project.ReleaseId);
                        var previousEnvs = deployments.Select(d => d.EnvironmentId);
                        foreach (var phase in lifeCyle.Phases)
                        {
                            if (!phase.Optional)
                            {
                                if (phase.MinimumEnvironmentsBeforePromotion == 0)
                                {
                                    if (!previousEnvs.All(e => phase.OptionalDeploymentTargetEnvironmentIds.Contains(e)) && !phase.OptionalDeploymentTargetEnvironmentIds.Contains(deployment.EnvironmentId))
                                    {
                                        phaseCheck = false;
                                        break;
                                    }
                                }
                                else
                                {
                                    if (phase.MinimumEnvironmentsBeforePromotion > previousEnvs.Intersect(phase.OptionalDeploymentTargetEnvironmentIds).Count())
                                    {
                                        phaseCheck = false;
                                        break;
                                    }
                                }
                            }
                        }
                        safe = phaseCheck;
                    }
                    if (!safe)
                    {
                        _log.Info($"Lifecycle safety check for {project.ProjectName} failed!");
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
            uiLogger.WriteLine("Starting deployment!");
            var failedProjects = new Dictionary<ProjectDeployment, TaskDetails>();

            var taskRegister = new Dictionary<string, TaskDetails>();
            var projectRegister = new Dictionary<string, ProjectDeployment>();

            foreach (var project in deployment.ProjectDeployments)
            {
                Release result;
                if (string.IsNullOrEmpty(project.ReleaseId))
                {
                    uiLogger.WriteLine("Creating a release for project " + project.ProjectName + "... ");
                    result = await _helper.Releases.CreateRelease(project, deployment.FallbackToDefaultChannel);
                }
                else
                {
                    uiLogger.WriteLine("Fetching existing release for project " + project.ProjectName + "... ");
                    result = await _helper.Releases.GetRelease(project.ReleaseId);
                }

                uiLogger.WriteLine("Creating deployment task for " + result.Version + " to " + deployment.EnvironmentName);
                var deployResult = await _helper.Deployments.CreateDeploymentTask(project, deployment.EnvironmentId, result.Id);
                uiLogger.WriteLine("Created");

                var taskDeets = await _helper.Deployments.GetTaskDetails(deployResult.TaskId);
                //taskDeets = await StartDeployment(uiLogger, taskDeets, !deployment.DeployAsync);
                if (deployment.DeployAsync)
                {
                    taskRegister.Add(taskDeets.TaskId, taskDeets);
                    projectRegister.Add(taskDeets.TaskId, project);
                }
                else
                {
                    if (taskDeets.State == TaskStatus.Failed)
                    {
                        uiLogger.WriteLine("Failed deploying " + project.ProjectName);
                        failedProjects.Add(project, taskDeets);
                    }
                    uiLogger.WriteLine("Deployed!");
                    uiLogger.WriteLine("Full Log: " + Environment.NewLine +
                                       await _helper.Deployments.GetTaskRawLog(taskDeets.TaskId));
                }
            }


            // This needs serious improvement.
            if (deployment.DeployAsync)
            {
                await DeployAsync(uiLogger, failedProjects, taskRegister, projectRegister);
            }

            if (failedProjects.Any())
            {
                uiLogger.WriteLine("Some projects didn't deploy successfully: ");
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
                    uiLogger.WriteLine(failure.Key.ProjectName + ": " + link);
                }
            }
            if (!suppressMessages)
            {
                uiLogger.WriteLine("Done deploying!" +
                                   (failedProjects.Any() ? " There were failures though. Check the log." : string.Empty));
            }
        }

        private async Task DeployAsync(IUiLogger uiLogger, Dictionary<ProjectDeployment, TaskDetails> failedProjects, Dictionary<string, TaskDetails> taskRegister, Dictionary<string, ProjectDeployment> projectRegister)
        {
            var done = false;
            var totalCount = taskRegister.Count();

            while (!done)
            {
                var tasks = await _helper.Deployments.GetDeploymentTasks(0, 100);
                foreach (var currentTask in taskRegister.ToList())
                {
                    var found = tasks.FirstOrDefault(t => t.TaskId == currentTask.Key);
                    if (found == null)
                    {
                        uiLogger.CleanCurrentLine();
                        uiLogger.WriteLine($"Couldn't find {currentTask.Key} in the tasks list?");
                        taskRegister.Remove(currentTask.Key);
                    }
                    else
                    {
                        if (found.State == TaskStatus.Done)
                        {
                            var project = projectRegister[currentTask.Key];
                            uiLogger.CleanCurrentLine();
                            uiLogger.WriteLine($"{project.ProjectName} deployed successfully");
                            taskRegister.Remove(currentTask.Key);
                        }
                        else if (found.State == TaskStatus.Failed)
                        {
                            var finishedTask = await _helper.Deployments.GetTaskDetails(found.TaskId);
                            var project = projectRegister[currentTask.Key];
                            uiLogger.CleanCurrentLine();
                            uiLogger.WriteLine($"{currentTask.Key} failed to deploy with error: {found.ErrorMessage}");
                            failedProjects.Add(project, finishedTask);
                            taskRegister.Remove(currentTask.Key);
                        }
                    }
                }

                if (taskRegister.Count == 0)
                {
                    done = true;
                }

                uiLogger.WriteProgress(totalCount - taskRegister.Count, totalCount, $"Deploying the requested projects ({totalCount - taskRegister.Count} of {totalCount} done)...");

                await Task.Delay(3000);
            }
            uiLogger.StopAnimation();
            uiLogger.CleanCurrentLine();
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

        private static void WriteStatus(IUiLogger uiLogger, TaskDetails taskDeets)
        {
            if (taskDeets.State != TaskStatus.Queued)
            {
                if (taskDeets.PercentageComplete < 100)
                {
                    uiLogger.WriteLine("Current status: " + taskDeets.State + " Percentage: " +
                                       taskDeets.PercentageComplete + " estimated time remaining: " +
                                       taskDeets.TimeLeft);
                }
            }
            else
            {
                uiLogger.WriteLine("Currently queued... waiting");
            }
        }
    }
}