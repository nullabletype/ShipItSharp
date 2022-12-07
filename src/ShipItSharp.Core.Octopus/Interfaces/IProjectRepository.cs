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
    public interface IProjectRepository
    {
        Task<List<ProjectStub>> GetProjectStubs();
        Task<Project> GetProject(string idOrHref, string environment, string channelRange, string tag);
        Task<bool> ValidateProjectName(string name);
        Task<Project> ConvertProject(ProjectStub project, string env, string channelRange, string tag);
        Task<ProjectResource> GetProject(string projectId);
        Task<List<ProjectGroup>> GetFilteredProjectGroups(string filter);
    }
}