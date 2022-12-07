using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Logging.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class PromotionRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IDeployer _deployer;
        private readonly IOctopusHelper _helper;
        private readonly IUiLogger _uiLogger;

        private PromotionConfig _currentConfig;
        private IProgressBar _progressBar;

        public PromotionRunner(ILanguageProvider languageProvider, IOctopusHelper helper, IDeployer deployer, IUiLogger uiLogger)
        {
            _languageProvider = languageProvider;
            this._helper = helper;
            this._deployer = deployer;
            this._uiLogger = uiLogger;
        }

        public async Task<int> Run(PromotionConfig config, IProgressBar progressBar, Func<PromotionConfig, (List<Project> currentProjects, List<Project> targetProjects), IEnumerable<int>> setDeploymentProjects, Func<string, string> userPrompt)
        {
            _currentConfig = config;
            this._progressBar = progressBar;

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

        private EnvironmentDeployment GenerateEnvironmentDeployment(IEnumerable<int> indexes, List<Project> projects, List<Project> targetProjects)
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
                var currentTarget = targetProjects[index];

                if (current.CurrentRelease != null)
                {
                    deployment.ProjectDeployments.Add(new ProjectDeployment
                    {
                        ProjectId = currentTarget.ProjectId,
                        ProjectName = currentTarget.ProjectName,
                        LifeCycleId = currentTarget.LifeCycleId,
                        ReleaseId = current.CurrentRelease.Id
                    });
                }
            }

            return deployment;
        }

        private async Task<(List<Project> projects, List<Project> targetProjects)> GenerateProjectList()
        {
            var projects = new List<Project>();
            var targetProjects = new List<Project>();

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

                var project = await _helper.Projects.ConvertProject(projectStub, _currentConfig.SourceEnvironment.Id, null, null);
                var targetProject = await _helper.Projects.ConvertProject(projectStub, _currentConfig.DestinationEnvironment.Id, null, null);

                var currentRelease = project.CurrentRelease;
                var currentTargetRelease = targetProject.CurrentRelease;
                if (currentRelease == null)
                {
                    continue;
                }

                if ((currentTargetRelease != null) && (currentTargetRelease.Id == currentRelease.Id))
                {
                    project.Checked = false;
                }
                else
                {
                    project.Checked = true;
                }

                projects.Add(project);
                targetProjects.Add(targetProject);
            }

            _progressBar.CleanCurrentLine();

            return (projects, targetProjects);
        }
    }
}