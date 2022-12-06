using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories;

public class PackageRepository : IPackageRepository
{
    private OctopusHelper octopusHelper;

    public PackageRepository(OctopusHelper octopusHelper)
    {
        this.octopusHelper = octopusHelper;
    }

    public async Task<IList<PackageStep>> GetPackages(string projectIdOrHref, string versionRange, string tag, int take = 5) 
    {
        return await GetPackages(await octopusHelper.ProjectsInternal.GetProject(projectIdOrHref), versionRange, tag, take);
    }

    internal async Task<PackageIdResult> GetPackageId(ProjectResource project, string stepName, string actionName) 
    {
        var process = await octopusHelper.GetDeploymentProcess(project.DeploymentProcessId);
        if (process != null) {
            foreach (var step in process.Steps.Where(s => s.Name == stepName)) 
            {
                foreach (var action in step.Actions.Where(a => a.Name == actionName)) 
                {
                    if (action.Properties.ContainsKey("Octopus.Action.Package.FeedId") &&
                        action.Properties["Octopus.Action.Package.FeedId"].Value == "feeds-builtin") 
                    {
                        if (action.Properties.ContainsKey("Octopus.Action.Package.PackageId") &&
                            !string.IsNullOrEmpty(action.Properties["Octopus.Action.Package.PackageId"].Value)) 
                        {
                            var packageId = action.Properties["Octopus.Action.Package.PackageId"].Value;
                            if (!string.IsNullOrEmpty(packageId)) 
                            {
                                return new PackageIdResult 
                                {
                                    PackageId = packageId,
                                    StepName = step.Name,
                                    StepId = step.Id
                                };
                            }
                        }
                    }

                }
            }
        }
        return null;
    }

    internal async Task<IList<PackageIdResult>> GetPackages(ProjectResource project)
    {
        var results = new List<PackageIdResult>();
        var process = await octopusHelper.GetDeploymentProcess(project.DeploymentProcessId);
        if (process != null)
        {
            foreach (var step in process.Steps)
            {
                foreach (var action in step.Actions)
                {
                    if (action.Properties.ContainsKey("Octopus.Action.Package.FeedId") &&
                        action.Properties["Octopus.Action.Package.FeedId"].Value == "feeds-builtin")
                    {
                        if (action.Properties.ContainsKey("Octopus.Action.Package.PackageId") &&
                            !string.IsNullOrEmpty(action.Properties["Octopus.Action.Package.PackageId"].Value))
                        {
                            var packageId = action.Properties["Octopus.Action.Package.PackageId"].Value;
                            if (!string.IsNullOrEmpty(packageId))
                            {
                                results.Add(new PackageIdResult
                                {
                                    PackageId = packageId,
                                    StepName = step.Name,
                                    StepId = step.Id
                                });
                            }
                        }
                    }

                }
            }
        }
        return results;
    }

    internal async Task<IList<PackageStep>> GetPackages(ProjectResource project, string versionRange, string tag, int take = 5) 
    {
        var packageIdResult = await this.GetPackages(project);
        var allPackages = new List<PackageStep>();
        foreach (var package in packageIdResult)
        {
            if (package != null && !string.IsNullOrEmpty(package.PackageId))
            {
                var template = octopusHelper.cacheProvider.GetCachedObject<Href>("feeds-builtin");

                if (template == null)
                {
                    template =
                        (await octopusHelper.client.Repository.Feeds.Get("feeds-builtin", CancellationToken.None)).Links["SearchTemplate"];
                    octopusHelper.cacheProvider.CacheObject("feeds-builtin", template);
                }

                var param = (dynamic)new
                {
                    packageId = package.PackageId,
                    partialMatch = false,
                    includeMultipleVersions = true,
                    take,
                    includePreRelease = true,
                    versionRange,
                    preReleaseTag = tag
                };

                var packages = await octopusHelper.client.Get<List<PackageFromBuiltInFeedResource>>(template, param);

                var finalPackages = new List<PackageStub>();
                foreach (var currentPackage in packages)
                {
                    finalPackages.Add(ConvertPackage(currentPackage, package.StepName));
                }
                allPackages.Add(new PackageStep { AvailablePackages = finalPackages, StepName = package.StepName, StepId = package.StepId });
            }
        }

        return allPackages;
    }

    public async Task<PackageFull> GetFullPackage(PackageStub stub) 
    {
        var package = new PackageFull {
            Id = stub.Id,
            Version = stub.Version,
            StepName = stub.StepName
        };
        var template = (await octopusHelper.client.Repository.Feeds.Get("feeds-builtin", CancellationToken.None)).Links["NotesTemplate"];
        package.Message =
            await octopusHelper.client.Get<string>(template,
                new {
                    packageId = stub.Id,
                    version = stub.Version
                }, CancellationToken.None);
        return package;
    }

    internal async Task<PackageStub> ConvertPackage(ProjectResource project, SelectedPackage package)
    {
        var packageDetails = await GetPackageId(project, package.ActionName, package.ActionName);
        return new PackageStub { Version = package.Version, StepName = packageDetails.StepName, StepId = packageDetails.StepId, Id = packageDetails.PackageId };
    }

    private PackageStub ConvertPackage(PackageResource package, string stepName)
    {
        return new PackageStub {Id = package.PackageId, Version = package.Version, StepName = stepName, PublishedOn = package.Published.HasValue ? package.Published.Value.LocalDateTime : null };
    }
}

public class PackageIdResult
{
    public string PackageId { get; set; }
    public string StepName { get; set; }
    public string StepId { get; set; }
}