using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class ReleaseRepository : IReleaseRepository
    {
        private readonly OctopusHelper _octopusHelper;

        public ReleaseRepository(OctopusHelper octopusHelper)
        {
            this._octopusHelper = octopusHelper;
        }

        public async Task<(Release Release, Deployment.Models.Deployment Deployment)> GetReleasedVersion(string projectId, string envId)
        {
            var deployment =
                await _octopusHelper.Client.Repository.Deployments.FindOne(resource => _octopusHelper.DeploymentsInternal.Search(resource, projectId, envId), pathParameters: new { take = 1, projects = projectId, environments = envId }, cancellationToken: CancellationToken.None, path: null);
            if (deployment != null)
            {
                var release = await GetReleaseInternal(deployment.ReleaseId);
                if (release != null)
                {
                    return (Release: await ConvertRelease(release), Deployment: _octopusHelper.DeploymentsInternal.ConvertDeployment(deployment));
                }
            }
            return (new Release { Id = "", Version = "None" }, new Deployment.Models.Deployment());
        }

        public async Task<bool> UpdateReleaseVariables(string releaseId)
        {
            var release = await _octopusHelper.Client.Repository.Releases.Get(releaseId, CancellationToken.None);
            if (release == null)
            {
                return false;
            }

            await _octopusHelper.Client.Repository.Releases.SnapshotVariables(release, CancellationToken.None);
            return true;
        }

        public async Task<Release> GetRelease(string releaseIdOrHref)
        {
            return await ConvertRelease(await GetReleaseInternal(releaseIdOrHref));
        }

        public async Task<Release> GetRelease(string name, Project project)
        {
            var projectRes = await _octopusHelper.Client.Repository.Projects.Get(project.ProjectId, CancellationToken.None);
            ReleaseResource release;
            try
            {
                release = await _octopusHelper.Client.Repository.Projects.GetReleaseByVersion(projectRes, name, CancellationToken.None);
            }
            catch
            {
                return null;
            }
            if (release == null)
            {
                return null;
            }
            return await ConvertRelease(release);
        }

        public async Task<Release> GetLatestRelease(Project project, string channelName)
        {
            ReleaseResource release;
            try
            {
                var projectRes = await _octopusHelper.Client.Repository.Projects.Get(project.ProjectId, CancellationToken.None);
                var channelRes = await _octopusHelper.Client.Repository.Channels.FindByName(projectRes, channelName);
                if (channelRes == null)
                {
                    return null;
                }
                release = (await _octopusHelper.Client.Repository.Channels.GetReleases(channelRes, 0, 1)).Items.FirstOrDefault();
            }
            catch
            {
                return null;
            }
            if (release == null)
            {
                return null;
            }
            return await ConvertRelease(release);
        }

        public async Task<Release> CreateRelease(ProjectDeployment project, bool ignoreChannelRules = false)
        {
            var user = await _octopusHelper.Client.Repository.Users.GetCurrent();
            var split = project.Packages.First().PackageName.Split('.');
            var releaseName = project.ReleaseVersion ?? split[0] + "." + split[1] + ".i";
            if ((project.ReleaseVersion == null) && !string.IsNullOrEmpty(project.ChannelName))
            {
                releaseName += "-" + project.ChannelName;
            }
            var release = new ReleaseResource
            {
                Assembled = DateTimeOffset.UtcNow,
                ChannelId = project.ChannelId,
                LastModifiedBy = user.Username,
                LastModifiedOn = DateTimeOffset.UtcNow,
                ProjectId = project.ProjectId,
                ReleaseNotes = project.ReleaseMessage ?? string.Empty,
                Version = releaseName
            };
            foreach (var package in project.Packages)
            {
                release.SelectedPackages.Add(new SelectedPackage { Version = package.PackageName, ActionName = package.StepName });
            }
            var result =
                await _octopusHelper.Client.Repository.Releases.Create(release, ignoreChannelRules, CancellationToken.None);
            return new Release
            {
                Version = result.Version,
                Id = result.Id,
                ReleaseNotes = result.ReleaseNotes
            };
        }

        public async Task<(string error, bool success)> RenameRelease(string releaseId, string newReleaseVersion)
        {
            try
            {
                var release = await GetReleaseInternal(releaseId);
                release.Version = newReleaseVersion;
                await _octopusHelper.Client.Repository.Releases.Modify(release, CancellationToken.None);
            }
            catch (Exception e)
            {
                return (e.Message, false);
            }

            return (string.Empty, true);
        }

        internal async Task<Release> ConvertRelease(ReleaseResource release)
        {
            var project = await _octopusHelper.Projects.GetProject(release.ProjectId);
            var packages =
                release.SelectedPackages.Select(
                    async p => await _octopusHelper.PackagesInternal.ConvertPackage(project, p)).Select(t => t.Result).ToList();
            return new Release
            {
                Id = release.Id,
                ProjectId = release.ProjectId,
                Version = release.Version,
                SelectedPackages = packages,
                DisplayPackageVersion = packages.Any() ? packages.First().Version : string.Empty,
                LastModifiedBy = release.LastModifiedBy,
                LastModifiedOn = release.LastModifiedOn,
                ReleaseNotes = release.ReleaseNotes
            };
        }

        internal async Task<ReleaseResource> GetReleaseInternal(string releaseId)
        {
            var cached = _octopusHelper.CacheProvider.GetCachedObject<ReleaseResource>(releaseId);
            if (cached == default(ReleaseResource))
            {
                var release = await _octopusHelper.Client.Repository.Releases.Get(releaseId, CancellationToken.None);
                _octopusHelper.CacheProvider.CacheObject(release.Id, release);
                return release;
            }
            return cached;
        }
    }
}