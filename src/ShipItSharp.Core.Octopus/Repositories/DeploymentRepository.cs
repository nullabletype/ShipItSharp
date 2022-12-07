using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories;

public class DeploymentRepository : IDeploymentRepository
{
    private OctopusHelper octopusHelper;

    public DeploymentRepository(OctopusHelper octopusHelper)
    {
        this.octopusHelper = octopusHelper;
    }

    public async Task<Deployment> CreateDeploymentTask(ProjectDeployment project, string environmentId, string releaseId) 
    {
        var user = await octopusHelper.client.Repository.Users.GetCurrent();
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
            ReleaseId = releaseId,
        };
        if (project.RequiredVariables != null)
        {
            foreach (var variable in project.RequiredVariables)
            {
                deployment.FormValues.Add(variable.Id, variable.Value);
            }
        }
        var deployResult = await octopusHelper.client.Repository.Deployments.Create(deployment, CancellationToken.None);
        return new Deployment {
            TaskId = deployResult.TaskId
        };
    }

    public async Task<IEnumerable<TaskStub>> GetDeploymentTasks(int skip, int take) 
    {
        //var taskDeets = await client.Repository.Tasks.FindAll(pathParameters: new { skip, take, name = "Deploy" });

        var taskDeets = await octopusHelper.client.Get<ResourceCollection<TaskResource>>((await octopusHelper.client.Repository.LoadRootDocument(CancellationToken.None)).Links["Tasks"], new { skip, take, name = "Deploy" }, CancellationToken.None);

        var tasks = new List<TaskStub>();

        foreach (var currentTask in taskDeets.Items) 
        {
            String deploymentId = null;
            if(currentTask.Arguments != null && currentTask.Arguments.ContainsKey("DeploymentId"))
            {
                deploymentId = currentTask.Arguments["DeploymentId"].ToString();
            }
            tasks.Add(new TaskStub 
            {
                State = currentTask.State == TaskState.Success ? Models.TaskStatus.Done :
                    currentTask.State == TaskState.Executing ? Models.TaskStatus.InProgress :
                    currentTask.State == TaskState.Queued ? Models.TaskStatus.Queued : Models.TaskStatus.Failed,
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

    public async Task<IEnumerable<Deployment>> GetDeployments(string[] deploymentIds)
    {
        var deployments = new List<Deployment>();
        foreach (var deploymentId in deploymentIds.Distinct())
        {
            var deployment = await octopusHelper.client.Repository.Deployments.Get(deploymentId, CancellationToken.None);
            deployments.Add(ConvertDeployment(deployment));
        }

        return deployments;
    }

    public async Task<IEnumerable<Deployment>> GetDeployments(string releaseId)
    {
        if(string.IsNullOrEmpty(releaseId))
        {
            return Array.Empty<Deployment>();
        }
        var deployments = await octopusHelper.client.Repository.Releases.GetDeployments(await octopusHelper.ReleasesInternal.GetReleaseInternal(releaseId), 0, 100, CancellationToken.None);
        return deployments.Items.ToList().Select(ConvertDeployment);
    }

    public bool Search(DeploymentResource deploymentResource, string projectId, string envId)
    {
        return deploymentResource.ProjectId == projectId && deploymentResource.EnvironmentId == envId;
    }

    internal Deployment ConvertDeployment(DeploymentResource dep)
    {
        return new Deployment
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
        var cached = octopusHelper.cacheProvider.GetCachedObject<DeploymentProcessResource>(deploymentProcessId);
        if (cached == default(DeploymentProcessResource))
        {
            var deployment = await octopusHelper.client.Repository.DeploymentProcesses.Get(deploymentProcessId, CancellationToken.None);
            octopusHelper.cacheProvider.CacheObject(deployment.Id, deployment);
            return deployment;
        }
        return cached;
    }
    
    public async Task<TaskDetails> GetTaskDetails(string taskId) 
    {
        var task = await octopusHelper.client.Repository.Tasks.Get(taskId, CancellationToken.None);
        var taskDeets = await octopusHelper.client.Repository.Tasks.GetDetails(task);

        return new TaskDetails 
        {
            PercentageComplete = taskDeets.Progress.ProgressPercentage,
            TimeLeft = taskDeets.Progress.EstimatedTimeRemaining,
            State = taskDeets.Task.State == TaskState.Success ? Models.TaskStatus.Done :
                taskDeets.Task.State == TaskState.Executing ? Models.TaskStatus.InProgress :
                taskDeets.Task.State == TaskState.Queued ? Models.TaskStatus.Queued : Models.TaskStatus.Failed,
            TaskId = taskId,
            Links = taskDeets.Links.ToDictionary(l => l.Key, l => l.Value.ToString())
        };
    }
    
    public async Task<string> GetTaskRawLog(string taskId) 
    {
        var task = await octopusHelper.client.Repository.Tasks.Get(taskId, CancellationToken.None);
        return await octopusHelper.client.Repository.Tasks.GetRawOutputLog(task);
    }
}