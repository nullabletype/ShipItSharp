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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Utilities;

namespace ShipItSharp.Core.JobRunners
{
    public class DeployRunner
    {
        private readonly IDeployer _deployer;
        private readonly IOctopusHelper _helper;
        private readonly ILanguageProvider _languageProvider;
        private readonly IUiLogger _uiLogger;

        private DeployConfig _currentConfig;
        private IProgressBar _progressBar;

        public DeployRunner(ILanguageProvider languageProvider, IOctopusHelper helper, IDeployer deployer, IUiLogger uiLogger)
        {
            _languageProvider = languageProvider;
            _helper = helper;
            _deployer = deployer;
            _uiLogger = uiLogger;
        }

        public async Task<int> Run(DeployConfig config, IProgressBar progressBar, List<ProjectStub> projectStubs, Func<DeployConfig, List<Project>, IEnumerable<int>> setDeploymentProjects, Func<string, string> userPrompt, Func<string, string> promptForReleaseName)
        {
            _currentConfig = config;
            _progressBar = progressBar;
            var groupIds = new List<string>();

            if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
            {
                progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds =
                    (await _helper.Projects.GetFilteredProjectGroups(_currentConfig.GroupFilter))
                    .Select(g => g.Id).ToList();
            }

            progressBar.CleanCurrentLine();
            var projects = await ConvertProjectStubsToProjects(projectStubs, groupIds);
            progressBar.CleanCurrentLine();

            var deployment = await GenerateDeployment(projects, setDeploymentProjects);
            var result = await _deployer.CheckDeployment(deployment);

            if (!result.Success)
            {
                Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Error") + result.ErrorMessage);
                return -1;
            }

            SetReleaseName(promptForReleaseName, deployment);

            _deployer.FillRequiredVariables(deployment.ProjectDeployments, userPrompt, _currentConfig.RunningInteractively);

            deployment.FallbackToDefaultChannel = _currentConfig.FallbackToDefaultChannel;

            if (!string.IsNullOrEmpty(_currentConfig.SaveProfile))
            {
                SaveProfile(deployment);
            }
            else
            {
                await _deployer.StartJob(deployment, _uiLogger);
            }

            return 0;
        }

        private void SetReleaseName(Func<string, string> promptForReleaseName, EnvironmentDeployment deployment)
        {
            var releaseName = _currentConfig.ReleaseName;

            if (_currentConfig.RunningInteractively && string.IsNullOrEmpty(_currentConfig.ReleaseName))
            {
                releaseName = promptForReleaseName(_languageProvider.GetString(LanguageSection.UiStrings, "ReleaseNamePrompt"));
            }

            if (!string.IsNullOrEmpty(releaseName))
            {
                foreach (var project in deployment.ProjectDeployments)
                {
                    project.ReleaseVersion = releaseName;
                }
            }
        }

        private async Task<EnvironmentDeployment> GenerateDeployment(List<Project> projects, Func<DeployConfig, List<Project>, IEnumerable<int>> setDeploymentProjects)
        {
            var indexes = new List<int>();

            if (_currentConfig.RunningInteractively)
            {
                indexes.AddRange(setDeploymentProjects(_currentConfig, projects));
                if (!indexes.Any())
                {
                    Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NothingSelected"));
                    return null;
                }
            }
            else
            {
                for (var i = 0; i < projects.Count(); i++)
                {
                    if (projects[i].Checked)
                    {
                        indexes.Add(i);
                    }
                }
            }

            var deployment = await PrepareEnvironmentDeployment(projects, indexes);

            return deployment;
        }

        private async Task<List<Project>> ConvertProjectStubsToProjects(List<ProjectStub> projectStubs, List<string> groupIds)
        {
            var projects = new List<Project>();
            var filteredStubs = projectStubs.ToList();

            if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
            {
                filteredStubs = filteredStubs.Where(p => groupIds.Contains((p.ProjectGroupId))).ToList();
            }

            await Parallel.ForEachAsync(filteredStubs, new ParallelOptions { MaxDegreeOfParallelism = 5 }, async (projectStub, _) =>
            {
                {
                    _progressBar.WriteProgress(filteredStubs.IndexOf(projectStub) + 1, filteredStubs.Count(),
                        string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));

                    var channel = await _helper.Channels.GetChannelByName(projectStub.ProjectId, _currentConfig.Channel);
                    var project = await _helper.Projects.ConvertProject(projectStub, _currentConfig.Environment.Id, channel?.VersionRange, channel?.VersionTag);
                    var currentPackages = project.CurrentRelease.SelectedPackages;
                    project.Checked = false;
                    if (project.SelectedPackageStubs != null)
                    {
                        foreach (var packageStep in project.AvailablePackages)
                        {
                            var stub = packageStep.SelectedPackage;
                            if (stub == null)
                            {
                                if (_currentConfig.DefaultFallbackChannel != null)
                                {
                                    var defaultChannel = await _helper.Channels.GetChannelByName(projectStub.ProjectId, _currentConfig.DefaultFallbackChannel);
                                    project = await _helper.Projects.ConvertProject(projectStub, _currentConfig.Environment.Id, defaultChannel?.VersionRange, defaultChannel?.VersionTag);
                                    stub = project.AvailablePackages.FirstOrDefault(p => p.StepId == packageStep.StepId)?.SelectedPackage;
                                }
                            }
                            var matchingCurrent = currentPackages.FirstOrDefault(p => p.StepId == packageStep.StepId);
                            if ((matchingCurrent != null) && (stub != null))
                            {
                                project.Checked = matchingCurrent.Version != stub.Version;
                                break;
                            }
                            if (stub == null)
                            {
                                project.Checked = false;
                            }
                            project.Checked = true;
                            break;
                        }
                    }

                    projects.Add(project);
                }
            });

            return projects;
        }

        private void SaveProfile(EnvironmentDeployment deployment)
        {
            foreach (var project in deployment.ProjectDeployments)
            {
                foreach (var package in project.Packages)
                {
                    package.PackageId = "latest";
                    package.PackageName = "latest";
                }
            }
            var content = StandardSerialiser.SerializeToJsonNet(deployment, true);
            File.WriteAllText(_currentConfig.SaveProfile, content);
            Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "ProfileSaved"), _currentConfig.SaveProfile);
        }

        private async Task<EnvironmentDeployment> PrepareEnvironmentDeployment(IList<Project> projects, IEnumerable<int> indexes = null)
        {
            var deployment = new EnvironmentDeployment
            {
                ChannelName = _currentConfig.Channel,
                DeployAsync = true,
                EnvironmentId = _currentConfig.Environment.Id,
                EnvironmentName = _currentConfig.Environment.Name
            };

            var count = 0;

            if (_currentConfig.ForceRedeploy)
            {
                var projectsWithAvailablePackages = projects.Where(p => p.AvailablePackages.Any());
                foreach (var project in projectsWithAvailablePackages)
                {
                    _progressBar.WriteProgress(count++, projectsWithAvailablePackages.Count(), string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "BuildingDeploymentJob"), project.ProjectName));
                    deployment.ProjectDeployments.Add(await GenerateProjectDeployment(project));
                }
            }
            else
            {
                foreach (var index in indexes)
                {
                    var current = projects[index];

                    if (current.AvailablePackages.Any())
                    {
                        _progressBar.WriteProgress(count++, indexes.Count(), string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "BuildingDeploymentJob"), projects[index].ProjectName));
                        deployment.ProjectDeployments.Add(await GenerateProjectDeployment(current));
                    }
                }
            }

            _progressBar.CleanCurrentLine();

            return deployment;
        }

        private async Task<ProjectDeployment> GenerateProjectDeployment(Project current)
        {

            if (current.AvailablePackages == null)
            {
                Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NoPackagesFound"), current.ProjectName);
            }

            var projectChannel = await _helper.Channels.GetChannelByName(current.ProjectId, _currentConfig.Channel);
            if ((_currentConfig.DefaultFallbackChannel != null) && (projectChannel == null))
            {
                projectChannel = await _helper.Channels.GetChannelByName(current.ProjectId, _currentConfig.DefaultFallbackChannel);
            }

            return new ProjectDeployment
            {
                ProjectId = current.ProjectId,
                ProjectName = current.ProjectName,
                Packages = current.AvailablePackages.Where(x => x.SelectedPackage != null).Select(x => new PackageDeployment
                {
                    PackageId = x.SelectedPackage.Id,
                    PackageName = x.SelectedPackage.Version,
                    StepId = x.StepId,
                    StepName = x.StepName
                }).ToList(),
                ChannelId = projectChannel?.Id,
                ChannelVersionRange = projectChannel?.VersionRange,
                ChannelVersionTag = projectChannel?.VersionTag,
                ChannelName = projectChannel?.Name,
                LifeCycleId = current.LifeCycleId,
                RequiredVariables = current.RequiredVariables?.Select(r => new RequiredVariableDeployment { Id = r.Id, ExtraOptions = r.ExtraOptions, Name = r.Name, Type = r.Type, Value = r.Value }).ToList()
            };
        }
    }
}