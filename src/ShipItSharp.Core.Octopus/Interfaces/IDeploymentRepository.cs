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