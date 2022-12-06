using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories;

public class ProjectRepository : IProjectRepository
{
    private OctopusHelper octoClient;

    public ProjectRepository(OctopusHelper client)
    {
        octoClient = client;
    }

    public async Task<List<ProjectStub>> GetProjectStubs() 
    {
        var projects = await octoClient.client.Repository.Projects.GetAll(CancellationToken.None);
        var converted = new List<ProjectStub>();
        foreach (var project in projects) {
            octoClient.cacheProvider.CacheObject(project.Id, project);
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
        var project = await octoClient.client.Repository.Projects.FindOne(resource => resource.Name == name, CancellationToken.None);
        return project != null;
    }

    private async Task<Project> ConvertProject(ProjectResource project, string env, string channelRange, string tag)
    {
        var packages = await octoClient.PackagesInternal.GetPackages(project, channelRange, tag);
        List<RequiredVariable> requiredVariables = await octoClient.VariablesInternal.GetVariables(project.VariableSetId);
        return new Project
        {
            CurrentRelease = (await octoClient.GetReleasedVersion(project.Id, env)).Release,
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
        return new ProjectStub {
            ProjectName = project.Name,
            ProjectId = project.Id,
            Checked = true,
            ProjectGroupId = project.ProjectGroupId,
            LifeCycleId = project.LifecycleId
        };
    }
    
    public async Task<Project> ConvertProject(ProjectStub project, string env, string channelRange, string tag)
    {
        var projectRes = await octoClient.ProjectsInternal.GetProject(project.ProjectId);
        var packages = channelRange == null ? null : await octoClient.PackagesInternal.GetPackages(projectRes, channelRange, tag);
        List<RequiredVariable> requiredVariables = await octoClient.VariablesInternal.GetVariables(projectRes.VariableSetId);
        return new Project {
            CurrentRelease = (await octoClient.GetReleasedVersion(project.ProjectId, env)).Release,
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
        var cached = octoClient.cacheProvider.GetCachedObject<ProjectResource>(projectId);
        if (cached == default(ProjectResource))
        {
            var project = await octoClient.client.Repository.Projects.Get(projectId, CancellationToken.None);
            octoClient.cacheProvider.CacheObject(project.Id, project);
            return project;
        }
        return cached;
    }
    
    public async Task<List<ProjectGroup>> GetFilteredProjectGroups(string filter) 
    {
        var groups = await octoClient.client.Repository.ProjectGroups.GetAll(CancellationToken.None);
        return groups.Where(g => g.Name.ToLower().Contains(filter.ToLower())).Select(ConvertProjectGroup).ToList();
    }
    
    private ProjectGroup ConvertProjectGroup(ProjectGroupResource projectGroup)
    {
        return new ProjectGroup {Id = projectGroup.Id, Name = projectGroup.Name};
    }
}