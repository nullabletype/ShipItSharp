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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Extensibility;
using Octopus.Client.Model;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class PackageRepository : IPackageRepository
    {
        private readonly OctopusHelper _octopusHelper;

        public PackageRepository(OctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public async Task<IList<PackageStep>> GetPackages(string projectIdOrHref, string versionRange, string tag, int take = 5)
        {
            return await GetPackages(await _octopusHelper.ProjectsInternal.GetProject(projectIdOrHref), versionRange, tag, take);
        }

        public async Task<PackageFull> GetFullPackage(PackageStub stub)
        {
            var package = new PackageFull
            {
                Id = stub.Id,
                Version = stub.Version,
                StepName = stub.StepName
            };
            var template = (await _octopusHelper.Client.Repository.Feeds.Get("feeds-builtin", CancellationToken.None)).Links["NotesTemplate"];
            package.Message =
                await _octopusHelper.Client.Get<string>(template,
                    new
                    {
                        packageId = stub.Id,
                        version = stub.Version
                    }, CancellationToken.None);
            return package;
        }

        internal async Task<PackageIdResult> GetPackageId(ProjectResource project, string stepName, string actionName)
        {
            var process = await _octopusHelper.DeploymentsInternal.GetDeploymentProcess(project.DeploymentProcessId);
            if (process != null)
            {
                foreach (var step in process.Steps.Where(s => s.Name == stepName))
                {
                    foreach (var action in step.Actions.Where(a => a.Name == actionName))
                    {
                        if (action.Properties.ContainsKey("Octopus.Action.Package.FeedId") &&
                            (action.Properties["Octopus.Action.Package.FeedId"].Value == "feeds-builtin"))
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
            var process = await _octopusHelper.DeploymentsInternal.GetDeploymentProcess(project.DeploymentProcessId);
            if (process != null)
            {
                foreach (var step in process.Steps)
                {
                    foreach (var action in step.Actions)
                    {
                        if (action.Properties.ContainsKey("Octopus.Action.Package.FeedId") &&
                            (action.Properties["Octopus.Action.Package.FeedId"].Value == "feeds-builtin"))
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
            var packageIdResult = await GetPackages(project);
            var allPackages = new List<PackageStep>();
            foreach (var package in packageIdResult)
            {
                if ((package != null) && !string.IsNullOrEmpty(package.PackageId))
                {
                    var template = _octopusHelper.CacheProvider.GetCachedObject<Href>("feeds-builtin");

                    if (template == null)
                    {
                        template =
                            (await _octopusHelper.Client.Repository.Feeds.Get("feeds-builtin", CancellationToken.None)).Links["SearchTemplate"];
                        _octopusHelper.CacheProvider.CacheObject("feeds-builtin", template);
                    }

                    var param = (dynamic) new
                    {
                        packageId = package.PackageId,
                        partialMatch = false,
                        includeMultipleVersions = true,
                        take,
                        includePreRelease = true,
                        versionRange,
                        preReleaseTag = tag
                    };

                    var packages = await _octopusHelper.Client.Get<List<PackageFromBuiltInFeedResource>>(template, param);

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

        internal async Task<PackageStub> ConvertPackage(ProjectResource project, SelectedPackage package)
        {
            var packageDetails = await GetPackageId(project, package.ActionName, package.ActionName);
            return new PackageStub { Version = package.Version, StepName = packageDetails.StepName, StepId = packageDetails.StepId, Id = packageDetails.PackageId };
        }

        private PackageStub ConvertPackage(PackageResource package, string stepName)
        {
            return new PackageStub { Id = package.PackageId, Version = package.Version, StepName = stepName, PublishedOn = package.Published.HasValue ? package.Published.Value.LocalDateTime : null };
        }
    }

    public class PackageIdResult
    {
        public string PackageId { get; set; }
        public string StepName { get; set; }
        public string StepId { get; set; }
    }
}