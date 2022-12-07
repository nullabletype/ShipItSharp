using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class ProjectRepository : IProjectRepository
    {
        private readonly OctopusHelper _octoClient;

        public ProjectRepository(OctopusHelper client)
        {
            _octoClient = client;
        }

        public async Task<List<ProjectStub>> GetProjectStubs()
        {
            var projects = await _octoClient.Client.Repository.Projects.GetAll(CancellationToken.None);
            var converted = new List<ProjectStub>();
            foreach (var project in projects)
            {
                _octoClient.CacheProvider.CacheObject(project.Id, project);
                converted.Add(ConvertProject(project));
            }
            return converted;
        }

        public async Task<Project> GetProject(string idOrHref, string environment, string channelRange, string tag)
        {
            return await ConvertProject(await GetProject(idOrHref), environment, channelRange, tag);
        }

        public async Task<bool> ValidateProjectName(string name)
        {
            var project = await _octoClient.Client.Repository.Projects.FindOne(resource => resource.Name == name, CancellationToken.None);
            return project != null;
        }

        public async Task<Project> ConvertProject(ProjectStub project, string env, string channelRange, string tag)
        {
            var projectRes = await _octoClient.ProjectsInternal.GetProject(project.ProjectId);
            var packages = channelRange == null ? null : await _octoClient.PackagesInternal.GetPackages(projectRes, channelRange, tag);
            var requiredVariables = await _octoClient.VariablesInternal.GetVariables(projectRes.VariableSetId);
            return new Project
            {
                CurrentRelease = (await _octoClient.ReleasesInternal.GetReleasedVersion(project.ProjectId, env)).Release,
                ProjectName = project.ProjectName,
                ProjectId = project.ProjectId,
                Checked = true,
                ProjectGroupId = project.ProjectGroupId,
                AvailablePackages = packages,
                LifeCycleId = project.LifeCycleId,
                RequiredVariables = requiredVariables
            };
        }

        public async Task<ProjectResource> GetProject(string projectId)
        {
            var cached = _octoClient.CacheProvider.GetCachedObject<ProjectResource>(projectId);
            if (cached == default(ProjectResource))
            {
                var project = await _octoClient.Client.Repository.Projects.Get(projectId, CancellationToken.None);
                _octoClient.CacheProvider.CacheObject(project.Id, project);
                return project;
            }
            return cached;
        }

        public async Task<List<ProjectGroup>> GetFilteredProjectGroups(string filter)
        {
            var groups = await _octoClient.Client.Repository.ProjectGroups.GetAll(CancellationToken.None);
            return groups.Where(g => g.Name.ToLower().Contains(filter.ToLower())).Select(ConvertProjectGroup).ToList();
        }

        private async Task<Project> ConvertProject(ProjectResource project, string env, string channelRange, string tag)
        {
            var packages = await _octoClient.PackagesInternal.GetPackages(project, channelRange, tag);
            var requiredVariables = await _octoClient.VariablesInternal.GetVariables(project.VariableSetId);
            return new Project
            {
                CurrentRelease = (await _octoClient.ReleasesInternal.GetReleasedVersion(project.Id, env)).Release,
                ProjectName = project.Name,
                ProjectId = project.Id,
                Checked = true,
                ProjectGroupId = project.ProjectGroupId,
                AvailablePackages = packages,
                LifeCycleId = project.LifecycleId,
                RequiredVariables = requiredVariables
            };
        }

        private ProjectStub ConvertProject(ProjectResource project)
        {
            return new ProjectStub
            {
                ProjectName = project.Name,
                ProjectId = project.Id,
                Checked = true,
                ProjectGroupId = project.ProjectGroupId,
                LifeCycleId = project.LifecycleId
            };
        }

        private ProjectGroup ConvertProjectGroup(ProjectGroupResource projectGroup)
        {
            return new ProjectGroup { Id = projectGroup.Id, Name = projectGroup.Name };
        }
    }
}