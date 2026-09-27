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
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class TaskRunner
    {
        private const int PageSize = 100;
        private readonly IOctopusHelper _octopusHelper;

        public TaskRunner(IOctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public Task<TaskOperationResult> PrioritiseQueuedTasks(string environmentName, IProgressBar progressBar, TaskRunnerMessages messages, bool skipConfirmation, ICommandInteraction interaction)
        {
            return Run(environmentName, progressBar, messages, skipConfirmation, interaction, taskId => _octopusHelper.Deployments.PrioritiseTask(taskId));
        }

        public Task<TaskOperationResult> PrioritiseQueuedTasks(string environmentName, IProgressBar progressBar, TaskRunnerMessages messages)
        {
            return PrioritiseQueuedTasks(environmentName, progressBar, messages, true, null);
        }

        public Task<TaskOperationResult> CancelQueuedTasks(string environmentName, IProgressBar progressBar, TaskRunnerMessages messages, bool skipConfirmation, ICommandInteraction interaction)
        {
            return Run(environmentName, progressBar, messages, skipConfirmation, interaction, taskId => _octopusHelper.Deployments.CancelTask(taskId));
        }

        public Task<TaskOperationResult> CancelQueuedTasks(string environmentName, IProgressBar progressBar, TaskRunnerMessages messages)
        {
            return CancelQueuedTasks(environmentName, progressBar, messages, true, null);
        }

        private async Task<TaskOperationResult> Run(string environmentName, IProgressBar progressBar, TaskRunnerMessages messages, bool skipConfirmation, ICommandInteraction interaction, Func<string, Task> taskAction)
        {
            var environment = await _octopusHelper.Environments.GetEnvironment(environmentName);
            if (environment == null)
            {
                return TaskOperationResult.NotFound();
            }

            progressBar.WriteStatusLine(messages.LoadingQueuedTasks);
            var queuedTasks = await GetQueuedDeploymentTasks();
            if (!queuedTasks.Any())
            {
                progressBar.CleanCurrentLine();
                return TaskOperationResult.Success(0, new List<string>());
            }

            progressBar.WriteStatusLine(messages.LoadingDeployments);
            var deployments = await _octopusHelper.Deployments.GetDeployments(queuedTasks.Select(t => t.DeploymentId).ToArray());
            var deploymentsByTask = deployments
                .Where(d => !string.IsNullOrEmpty(d.TaskId))
                .ToDictionary(d => d.TaskId);

            var matchingTasks = queuedTasks
                .Where(t => deploymentsByTask.TryGetValue(t.TaskId, out var deployment) && deployment.EnvironmentId == environment.Id)
                .ToList();

            progressBar.CleanCurrentLine();
            if (matchingTasks.Count > 0 && !skipConfirmation && !interaction.Confirm(string.Format(messages.Confirmation, matchingTasks.Count, environment.Name), false))
            {
                return TaskOperationResult.CancelledByOperator(queuedTasks.Count);
            }

            for (var index = 0; index < matchingTasks.Count; index++)
            {
                var task = matchingTasks[index];
                progressBar.WriteProgress(index + 1, matchingTasks.Count, string.Format(messages.ProcessingTask, task.TaskId));
                await taskAction(task.TaskId);
            }

            progressBar.CleanCurrentLine();
            return TaskOperationResult.Success(queuedTasks.Count, matchingTasks.Select(t => t.TaskId).ToList());
        }

        private async Task<List<TaskStub>> GetQueuedDeploymentTasks()
        {
            var tasks = new List<TaskStub>();
            var skip = 0;
            List<TaskStub> page;
            do
            {
                page = (await _octopusHelper.Deployments.GetDeploymentTasks(skip, PageSize)).ToList();

                tasks.AddRange(page.Where(t => t.State == ShipItSharp.Core.Deployment.Models.TaskStatus.Queued && !string.IsNullOrEmpty(t.DeploymentId)));
                skip += PageSize;
            } while (page.Count == PageSize);

            return tasks;
        }
    }

    public class TaskRunnerMessages
    {
        public string LoadingQueuedTasks { get; init; }
        public string LoadingDeployments { get; init; }
        public string ProcessingTask { get; init; }
        public string Confirmation { get; init; }
    }

    public class TaskOperationResult
    {
        private TaskOperationResult(bool found, bool cancelled, int scannedTaskCount, List<string> affectedTaskIds)
        {
            Found = found;
            Cancelled = cancelled;
            ScannedTaskCount = scannedTaskCount;
            AffectedTaskIds = affectedTaskIds;
        }

        public bool Found { get; }
        public bool Cancelled { get; }
        public int ScannedTaskCount { get; }
        public List<string> AffectedTaskIds { get; }

        public static TaskOperationResult NotFound()
        {
            return new TaskOperationResult(false, false, 0, new List<string>());
        }

        public static TaskOperationResult Success(int scannedTaskCount, List<string> affectedTaskIds)
        {
            return new TaskOperationResult(true, false, scannedTaskCount, affectedTaskIds);
        }

        public static TaskOperationResult CancelledByOperator(int scannedTaskCount)
        {
            return new TaskOperationResult(true, true, scannedTaskCount, new List<string>());
        }
    }
}
