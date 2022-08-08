using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Logging.Interfaces;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Octopus.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShipItSharp.Core.JobRunners
{
    public class DeploySpecificRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper helper;
        private readonly IDeployer deployer;
        private readonly IUiLogger uiLogger;

        private DeploySpecificConfig _currentConfig;
        private IProgressBar progressBar;

        public DeploySpecificRunner(ILanguageProvider languageProvider, IOctopusHelper helper, IDeployer deployer, IUiLogger uiLogger)
        {
            this._languageProvider = languageProvider;
            this.helper = helper;
            this.deployer = deployer;
            this.uiLogger = uiLogger;
        }

        public async Task<int> Run(DeploySpecificConfig config, IProgressBar progressBar, Func<DeploySpecificConfig, (List<Project> currentProjects, List<Release> targetReleases), IEnumerable<int>> setDeploymentProjects, Func<string, string> userPrompt)
        {
            this._currentConfig = config;
            this.progressBar = progressBar;

            var (projects, targetProjects) = await GenerateProjectList();

            List<int> indexes = new List<int>();

            if (config.RunningInteractively)
            {
                indexes.AddRange(setDeploymentProjects(_currentConfig, (projects, targetProjects)));
            }
            else 
            {
                for (int i = 0; i < projects.Count(); i++)
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

            var result = await this.deployer.CheckDeployment(deployment);
            if (!result.Success)
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Error") + result.ErrorMessage);
                return -1;
            }

            deployer.FillRequiredVariables(deployment.ProjectDeployments, userPrompt, _currentConfig.RunningInteractively);

            await this.deployer.StartJob(deployment, this.uiLogger);

            return 0;
        }

        private EnvironmentDeployment GenerateEnvironmentDeployment(IEnumerable<int> indexes, List<Project> projects, List<Release> targetReleases)
        {
            if (!indexes.Any())
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NothingSelected"));
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

            progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));
            var projectStubs = await helper.GetProjectStubs();

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
            {
                progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds =
                    (await helper.GetFilteredProjectGroups(_currentConfig.GroupFilter))
                    .Select(g => g.Id).ToList();
            }

            progressBar.CleanCurrentLine();

            foreach (var projectStub in projectStubs)
            {
                progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
                    String.Format(_languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId))
                    {
                        continue;
                    }
                }

                var project = await helper.ConvertProject(projectStub, _currentConfig.DestinationEnvironment.Id, null, null);

                var newRelease = await helper.GetRelease(this._currentConfig.ReleaseName, project);

                if (newRelease == null && this._currentConfig.FallbackToDefaultChannel)
                {
                    newRelease = await helper.GetLatestRelease(project, this._currentConfig.DefaultFallbackChannel);
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

            progressBar.CleanCurrentLine();

            return (projects, targetReleases);
        }
    }
}
