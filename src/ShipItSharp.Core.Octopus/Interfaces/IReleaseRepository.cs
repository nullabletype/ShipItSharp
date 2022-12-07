using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Models;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface IReleaseRepository
    {
        Task<(Release Release, Deployment.Models.Deployment Deployment)> GetReleasedVersion(string projectId, string envId);
        Task<bool> UpdateReleaseVariables(string releaseId);
        Task<Release> GetRelease(string releaseIdOrHref);
        Task<Release> GetRelease(string name, Project project);
        Task<Release> GetLatestRelease(Project project, string channelName);
        Task<Release> CreateRelease(ProjectDeployment project, bool ignoreChannelRules = false);
        Task<(string error, bool success)> RenameRelease(string releaseId, string newReleaseVersion);
    }
}