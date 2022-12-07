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
using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class DeploySpecificRunner
    {
        private readonly IDeployer _deployer;
        private readonly IOctopusHelper _helper;
        private readonly ILanguageProvider _languageProvider;
        private readonly IUiLogger _uiLogger;

        private DeploySpecificConfig _currentConfig;
        private IProgressBar _progressBar;

        public DeploySpecificRunner(ILanguageProvider languageProvider, IOctopusHelper helper, IDeployer deployer, IUiLogger uiLogger)
        {
            _languageProvider = languageProvider;
            _helper = helper;
            _deployer = deployer;
            _uiLogger = uiLogger;
        }

        public async Task<int> Run(DeploySpecificConfig config, IProgressBar progressBar, Func<DeploySpecificConfig, (List<Project> currentProjects, List<Release> targetReleases), IEnumerable<int>> setDeploymentProjects, Func<string, string> userPrompt)
        {
            _currentConfig = config;
            _progressBar = progressBar;

            var (projects, targetProjects) = await GenerateProjectList();

            var indexes = new List<int>();

            if (config.RunningInteractively)
            {
                indexes.AddRange(setDeploymentProjects(_currentConfig, (projects, targetProjects)));
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

            var deployment = GenerateEnvironmentDeployment(indexes, projects, targetProjects);

            if (deployment == null)
            {
                return -1;
            }

            var result = await _deployer.CheckDeployment(deployment);
            if (!result.Success)
            {
                Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Error") + result.ErrorMessage);
                return -1;
            }

            _deployer.FillRequiredVariables(deployment.ProjectDeployments, userPrompt, _currentConfig.RunningInteractively);

            await _deployer.StartJob(deployment, _uiLogger);

            return 0;
        }

        private EnvironmentDeployment GenerateEnvironmentDeployment(IEnumerable<int> indexes, List<Project> projects, List<Release> targetReleases)
        {
            if (!indexes.Any())
            {
                Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NothingSelected"));
                return null;
            }

            var deployment = new EnvironmentDeployment
            {
                ChannelName = string.Empty,
                DeployAsync = true,
                EnvironmentId = _currentConfig.DestinationEnvironment.Id,
                EnvironmentName = _currentConfig.DestinationEnvironment.Name
            };

            foreach (var index in indexes)
            {
                var current = projects[index];
                var release = targetReleases.FirstOrDefault(r => r.ProjectId == current.ProjectId);

                if (release != null)
                {
                    deployment.ProjectDeployments.Add(new ProjectDeployment
                    {
                        ProjectId = current.ProjectId,
                        ProjectName = current.ProjectName,
                        LifeCycleId = current.LifeCycleId,
                        ReleaseId = release.Id
                    });
                }
            }

            return deployment;
        }

        private async Task<(List<Project> projects, List<Release> targetReleases)> GenerateProjectList()
        {
            var projects = new List<Project>();
            var targetReleases = new List<Release>();

            _progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));
            var projectStubs = await _helper.Projects.GetProjectStubs();

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
            {
                _progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds =
                    (await _helper.Projects.GetFilteredProjectGroups(_currentConfig.GroupFilter))
                    .Select(g => g.Id).ToList();
            }

            _progressBar.CleanCurrentLine();

            foreach (var projectStub in projectStubs)
            {
                _progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
                    string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId))
                    {
                        continue;
                    }
                }

                var project = await _helper.Projects.ConvertProject(projectStub, _currentConfig.DestinationEnvironment.Id, null, null);

                var newRelease = await _helper.Releases.GetRelease(_currentConfig.ReleaseName, project);

                if ((newRelease == null) && _currentConfig.FallbackToDefaultChannel)
                {
                    newRelease = await _helper.Releases.GetLatestRelease(project, _currentConfig.DefaultFallbackChannel);
                }

                var currentRelease = project.CurrentRelease;
                var currentTargetRelease = newRelease;


                if (currentRelease == null)
                {
                    continue;
                }

                if (currentTargetRelease == null || currentTargetRelease.Id == currentRelease.Id)
                {
                    project.Checked = false;
                }
                else
                {
                    project.Checked = true;
                }

                projects.Add(project);
                if (newRelease != null)
                {
                    targetReleases.Add(newRelease);
                }
            }

            _progressBar.CleanCurrentLine();

            return (projects, targetReleases);
        }
    }
}