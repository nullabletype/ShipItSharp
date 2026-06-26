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


using System;
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
        private const string BuiltInFeedId = "feeds-builtin";
        private const string FeedIdPropertyName = "Octopus.Action.Package.FeedId";
        private const string PackageIdPropertyName = "Octopus.Action.Package.PackageId";
        private readonly OctopusHelper _octopusHelper;

        public PackageRepository(OctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public async Task<IList<PackageStep>> GetPackages(string projectIdOrHref, string versionRange, string tag, bool allowNoVersion = false, int take = 5)
        {
            return await GetPackages(await _octopusHelper.ProjectsInternal.GetProject(projectIdOrHref), versionRange, tag, allowNoVersion, take);
        }

        public async Task<PackageFull> GetFullPackage(PackageStub stub)
        {
            var feedId = GetFeedIdOrDefault(stub.FeedId);
            var package = new PackageFull
            {
                Id = stub.Id,
                FeedId = feedId,
                Version = stub.Version,
                StepName = stub.StepName
            };
            var template = (await _octopusHelper.Client.Repository.Feeds.Get(feedId, CancellationToken.None)).Links["NotesTemplate"];
            package.Message =
                await _octopusHelper.Client.Get<string>(template,
                    new
                    {
                        packageId = stub.Id,
                        version = stub.Version
                    }, CancellationToken.None);
            return package;
        }

        internal async Task<PackageIdResult> GetPackageId(ProjectResource project, string stepName, string actionName, string packageReferenceName = null)
        {
            var process = await _octopusHelper.DeploymentsInternal.GetDeploymentProcess(project.DeploymentProcessId);
            if (process != null)
            {
                foreach (var step in process.Steps.Where(s => string.IsNullOrEmpty(stepName) || s.Name == stepName))
                {
                    foreach (var action in step.Actions.Where(a => string.IsNullOrEmpty(actionName) || a.Name == actionName))
                    {
                        var package = GetPackageReferences(step, action)
                            .FirstOrDefault(p => PackageReferenceNameMatches(p.PackageReferenceName, packageReferenceName));
                        if (package != null)
                        {
                            return package;
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
                        results.AddRange(GetPackageReferences(step, action));
                    }
                }
            }
            return results;
        }

        internal async Task<IList<PackageStep>> GetPackages(ProjectResource project, string versionRange, string tag, bool allowNoChannel = false, int take = 5)
        {
            var packageIdResult = await GetPackages(project);
            var allPackages = new List<PackageStep>();
            foreach (var packageStepId in packageIdResult)
            {
                if ((packageStepId != null) && !string.IsNullOrEmpty(packageStepId.PackageId))
                {
                    if (versionRange == null && !allowNoChannel)
                    {
                        allPackages.Add(new PackageStep { AvailablePackages = new List<PackageStub>(), StepName = packageStepId.StepName, StepId = packageStepId.StepId });
                        continue; // If no versionRange specified, we likely have no channel so no packages
                    }
                    
                    var feedId = GetFeedIdOrDefault(packageStepId.FeedId);
                    var templateCacheKey = $"{feedId}:SearchTemplate";
                    var template = _octopusHelper.CacheProvider.GetCachedObject<Href>(templateCacheKey);

                    if (template == null)
                    {
                        template =
                            (await _octopusHelper.Client.Repository.Feeds.Get(feedId, CancellationToken.None)).Links["SearchTemplate"];
                        _octopusHelper.CacheProvider.CacheObject(templateCacheKey, template);
                    }

                    var param = (dynamic) new
                    {
                        packageId = packageStepId.PackageId,
                        partialMatch = false,
                        includeMultipleVersions = true,
                        take,
                        includePreRelease = true,
                        versionRange,
                        preReleaseTag = tag
                    };

                    var packages = await _octopusHelper.Client.Get<List<PackageResource>>(template, param);

                    var finalPackages = new List<PackageStub>();
                    foreach (var currentPackage in packages)
                    {
                        finalPackages.Add(ConvertPackage(currentPackage, packageStepId));
                    }
                    allPackages.Add(new PackageStep { AvailablePackages = finalPackages, StepName = packageStepId.StepName, StepId = packageStepId.StepId });
                }
            }

            return allPackages;
        }

        internal async Task<PackageStub> ConvertPackage(ProjectResource project, SelectedPackage package)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            var packageDetails = await GetPackageId(project, null, package.ActionName, package.PackageReferenceName);
            if (packageDetails == null)
            {
                throw new InvalidOperationException($"Could not find package details for action '{package.ActionName}'.");
            }
            return new PackageStub
            {
                Version = package.Version,
                StepName = packageDetails.StepName,
                StepId = packageDetails.StepId,
                ActionName = packageDetails.ActionName,
                PackageReferenceName = packageDetails.PackageReferenceName,
                FeedId = packageDetails.FeedId,
                Id = packageDetails.PackageId
            };
        }

        internal static IList<PackageIdResult> GetPackageReferences(DeploymentStepResource step, DeploymentActionResource action)
        {
            var packageReferences = action.Packages
                .Where(package => !string.IsNullOrEmpty(package.PackageId) && !string.IsNullOrEmpty(package.FeedId))
                .Select(package => new PackageIdResult
                {
                    PackageId = package.PackageId,
                    FeedId = package.FeedId,
                    StepName = step.Name,
                    StepId = step.Id,
                    ActionName = action.Name,
                    PackageReferenceName = package.Name
                })
                .ToList();

            if (packageReferences.Any())
            {
                return packageReferences;
            }

            if (!action.Properties.ContainsKey(PackageIdPropertyName) ||
                string.IsNullOrEmpty(action.Properties[PackageIdPropertyName].Value))
            {
                return new List<PackageIdResult>();
            }

            return new List<PackageIdResult>
            {
                new PackageIdResult
                {
                    PackageId = action.Properties[PackageIdPropertyName].Value,
                    FeedId = action.Properties.ContainsKey(FeedIdPropertyName)
                        ? action.Properties[FeedIdPropertyName].Value
                        : BuiltInFeedId,
                    StepName = step.Name,
                    StepId = step.Id,
                    ActionName = action.Name
                }
            };
        }

        private static bool PackageReferenceNameMatches(string candidate, string expected)
        {
            return string.IsNullOrEmpty(candidate)
                ? string.IsNullOrEmpty(expected)
                : string.Equals(candidate, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFeedIdOrDefault(string feedId)
        {
            return string.IsNullOrEmpty(feedId) ? BuiltInFeedId : feedId;
        }

        private static PackageStub ConvertPackage(PackageResource package, PackageIdResult packageDetails)
        {
            return new PackageStub
            {
                Id = package.PackageId,
                FeedId = packageDetails.FeedId,
                Version = package.Version,
                StepName = packageDetails.StepName,
                StepId = packageDetails.StepId,
                ActionName = packageDetails.ActionName,
                PackageReferenceName = packageDetails.PackageReferenceName,
                PublishedOn = package.Published.HasValue ? package.Published.Value.LocalDateTime : null
            };
        }
    }

    public class PackageIdResult
    {
        public string PackageId { get; set; }
        public string FeedId { get; set; }
        public string StepName { get; set; }
        public string StepId { get; set; }
        public string ActionName { get; set; }
        public string PackageReferenceName { get; set; }
    }
}
