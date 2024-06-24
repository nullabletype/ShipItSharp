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
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Octopus.Interfaces;
using TaskStatus = ShipItSharp.Core.Deployment.Models.TaskStatus;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class DeploymentRepository : IDeploymentRepository
    {
        private readonly OctopusHelper _octopusHelper;

        public DeploymentRepository(OctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public async Task<Deployment.Models.Deployment> CreateDeploymentTask(ProjectDeployment project, string environmentId, string releaseId)
        {
            var user = await _octopusHelper.Client.Repository.Users.GetCurrent();
            var deployment = new DeploymentResource
            {
                ChannelId = project.ChannelId,
                Comments = "Initiated by ShipItSharp",
                Created = DateTimeOffset.UtcNow,
                EnvironmentId = environmentId,
                LastModifiedBy = user.Username,
                LastModifiedOn = DateTimeOffset.UtcNow,
                Name = project.ProjectName + ":" + project.Packages?.First().PackageName,
                ProjectId = project.ProjectId,
                ReleaseId = releaseId
            };
            if (project.RequiredVariables != null)
            {
                foreach (var variable in project.RequiredVariables)
                {
                    deployment.FormValues.Add(variable.Id, variable.Value);
                }
            }
            var deployResult = await _octopusHelper.Client.Repository.Deployments.Create(deployment, CancellationToken.None);
            return new Deployment.Models.Deployment
            {
                TaskId = deployResult.TaskId
            };
        }

        public async Task<IEnumerable<TaskStub>> GetDeploymentTasks(int skip, int take)
        {
            //var taskDeets = await client.Repository.Tasks.FindAll(pathParameters: new { skip, take, name = "Deploy" });

            var taskDeets = await _octopusHelper.Client.Get<ResourceCollection<TaskResource>>((await _octopusHelper.Client.Repository.LoadRootDocument(CancellationToken.None)).Links["Tasks"], new { skip, take, name = "Deploy" }, CancellationToken.None);

            var tasks = new List<TaskStub>();

            foreach (var currentTask in taskDeets.Items)
            {
                string deploymentId = null;
                if ((currentTask.Arguments != null) && currentTask.Arguments.ContainsKey("DeploymentId"))
                {
                    deploymentId = currentTask.Arguments["DeploymentId"].ToString();
                }
                tasks.Add(new TaskStub
                {
                    State = currentTask.State == TaskState.Success ? TaskStatus.Done :
                        currentTask.State == TaskState.Executing ? TaskStatus.InProgress :
                        currentTask.State == TaskState.Queued ? TaskStatus.Queued : TaskStatus.Failed,
                    ErrorMessage = currentTask.ErrorMessage,
                    FinishedSuccessfully = currentTask.FinishedSuccessfully,
                    HasWarningsOrErrors = currentTask.HasWarningsOrErrors,
                    IsComplete = currentTask.IsCompleted,
                    TaskId = currentTask.Id,
                    Links = currentTask.Links.ToDictionary(l => l.Key, l => l.Value.ToString()),
                    DeploymentId = deploymentId
                });
            }

            return tasks;
        }

        public async Task<IEnumerable<Deployment.Models.Deployment>> GetDeployments(string[] deploymentIds)
        {
            var deployments = new List<Deployment.Models.Deployment>();
            foreach (var deploymentId in deploymentIds.Distinct())
            {
                var deployment = await _octopusHelper.Client.Repository.Deployments.Get(deploymentId, CancellationToken.None);
                deployments.Add(ConvertDeployment(deployment));
            }

            return deployments;
        }

        public async Task<IEnumerable<Deployment.Models.Deployment>> GetDeployments(string releaseId)
        {
            if (string.IsNullOrEmpty(releaseId))
            {
                return Array.Empty<Deployment.Models.Deployment>();
            }
            var deployments = await _octopusHelper.Client.Repository.Releases.GetDeployments(await _octopusHelper.ReleasesInternal.GetReleaseInternal(releaseId), 0, 100, CancellationToken.None);
            return deployments.Items.ToList().Select(ConvertDeployment);
        }

        public bool Search(DeploymentResource deploymentResource, string projectId, string envId)
        {
            return (deploymentResource.ProjectId == projectId) && (deploymentResource.EnvironmentId == envId);
        }

        public async Task<TaskDetails> GetTaskDetails(string taskId)
        {
            var task = await _octopusHelper.Client.Repository.Tasks.Get(taskId, CancellationToken.None);
            var taskDeets = await _octopusHelper.Client.Repository.Tasks.GetDetails(task, CancellationToken.None);

            return new TaskDetails
            {
                PercentageComplete = taskDeets.Progress.ProgressPercentage,
                TimeLeft = taskDeets.Progress.EstimatedTimeRemaining,
                State = taskDeets.Task.State == TaskState.Success ? TaskStatus.Done :
                    taskDeets.Task.State == TaskState.Executing ? TaskStatus.InProgress :
                    taskDeets.Task.State == TaskState.Queued ? TaskStatus.Queued : TaskStatus.Failed,
                TaskId = taskId,
                Links = taskDeets.Links.ToDictionary(l => l.Key, l => l.Value.ToString())
            };
        }

        public async Task<string> GetTaskRawLog(string taskId)
        {
            var task = await _octopusHelper.Client.Repository.Tasks.Get(taskId, CancellationToken.None);
            return await _octopusHelper.Client.Repository.Tasks.GetRawOutputLog(task, CancellationToken.None);
        }

        internal Deployment.Models.Deployment ConvertDeployment(DeploymentResource dep)
        {
            return new Deployment.Models.Deployment
            {
                EnvironmentId = dep.EnvironmentId,
                ReleaseId = dep.ReleaseId,
                TaskId = dep.TaskId,
                LastModifiedBy = dep.LastModifiedBy,
                LastModifiedOn = dep.LastModifiedOn,
                Created = dep.Created
            };
        }

        internal async Task<DeploymentProcessResource> GetDeploymentProcess(string deploymentProcessId)
        {
            var cached = _octopusHelper.CacheProvider.GetCachedObject<DeploymentProcessResource>(deploymentProcessId);
            if (cached == default(DeploymentProcessResource))
            {
                var deployment = await _octopusHelper.Client.Repository.DeploymentProcesses.Get(deploymentProcessId, CancellationToken.None);
                _octopusHelper.CacheProvider.CacheObject(deployment.Id, deployment);
                return deployment;
            }
            return cached;
        }
    }
}