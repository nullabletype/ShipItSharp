using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Deployment.Models;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface IDeploymentRepository
    {
        Task<Deployment.Models.Deployment> CreateDeploymentTask(ProjectDeployment project, string environmentId, string releaseId);
        Task<IEnumerable<TaskStub>> GetDeploymentTasks(int skip, int take);
        Task<IEnumerable<Deployment.Models.Deployment>> GetDeployments(string[] deploymentIds);
        Task<IEnumerable<Deployment.Models.Deployment>> GetDeployments(string releaseId);
        bool Search(DeploymentResource deploymentResource, string projectId, string envId);
        Task<TaskDetails> GetTaskDetails(string taskId);
        Task<string> GetTaskRawLog(string taskId);
    }
}