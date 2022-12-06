using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Models;

namespace ShipItSharp.Core.Octopus.Interfaces;

public interface IProjectRepository
{
    Task<List<ProjectStub>> GetProjectStubs();
    Task<Project> GetProject(string idOrHref, string environment, string channelRange, string tag);
    Task<bool> ValidateProjectName(string name);
    Task<Project> ConvertProject(ProjectStub project, string env, string channelRange, string tag);
    Task<ProjectResource> GetProject(string projectId);
    Task<List<ProjectGroup>> GetFilteredProjectGroups(string filter);
}