#region copyright
/*
    ShipItSharp Deployment Coordinator. Provides extra tooling to help 
    deploy software through Octopus Deploy.

    Copyright (C) 2018  Steven Davies

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion


using System.Collections.Generic;
using System.Threading.Tasks;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Models.Variables;
using ShipItSharp.Core.Octopus.Repositories;
using static ShipItSharp.Core.Octopus.OctopusHelper;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface IOctopusHelper
    {
        IPackageRepository Packages { get; }
        IProjectRepository Projects { get; }
        IVariableRepository Variables { get; }
        IChannelRepository Channels { get; }
        IEnvironmentRepository Environments { get; }

        void SetCacheImplementation(ICacheObjects cache, int cacheTimeout);
        Task<(Release Release, Deployment Deployment)> GetReleasedVersion(string projectId, string envId);
        Task<Release> GetRelease(string releaseIdOrHref);
        Task<TaskDetails> GetTaskDetails(string taskId);
        Task<IEnumerable<TaskStub>> GetDeploymentTasks(int skip, int take);
        Task<string> GetTaskRawLog(string taskId);
        Task<Release> CreateRelease(ProjectDeployment project, bool ignoreChannelRules = false);
        Task<Deployment> CreateDeploymentTask(ProjectDeployment project, string environmentId, string releaseId);
        Task<LifeCycle> GetLifeCycle(string idOrHref);
        Task<IEnumerable<Deployment>> GetDeployments(string releaseId);
        Task<(string error, bool success)> RenameRelease(string releaseId, string newReleaseVersion);
        Task<bool> UpdateReleaseVariables(string releaseId);
        Task RemoveEnvironmentsFromTeams(string envId);
        Task RemoveEnvironmentsFromLifecycles(string envId);
        Task AddEnvironmentToTeam(string envId, string teamId);
        Task<IEnumerable<Deployment>> GetDeployments(string[] deploymentIds);
        Task<(bool Success, LifecycleErrorType ErrorType, string Error)> AddEnvironmentToLifecyclePhase(string envId, string lcId, int phaseId, bool automatic);
        Task<Release> GetRelease(string name, Project project);
        Task<Release> GetLatestRelease(Project project, string channelName);
    }
}