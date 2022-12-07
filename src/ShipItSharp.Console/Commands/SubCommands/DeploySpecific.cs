#region copyright
/*
    ShipItSharp Deployment Coordinator. Provides extra tooling to help 
    deploy software through Octopus Deploy.

    Copyright (C) 2018  Steven Davies

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion


using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class DeploySpecific : BaseCommand
    {
        private readonly IConfiguration _configuration;
        private readonly IProgressBar _progressBar;
        private readonly DeploySpecificRunner _runner;

        public DeploySpecific(IOctopusHelper octopusHelper, IProgressBar progressBar, ILanguageProvider languageProvider, DeploySpecificRunner runner, IConfiguration configuration) : base(octopusHelper, languageProvider)
        {
            this._progressBar = progressBar;
            this._runner = runner;
            this._configuration = configuration;
        }
        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "specific";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(DeployOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.DefaultFallback, command.Option("-d|--fallbacktodefault", LanguageProvider.GetString(LanguageSection.OptionsStrings, "FallbackToDefault"), CommandOptionType.NoValue));
            AddToRegister(DeployOptionNames.ReleaseName, command.Option("-r|--releasename", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ReleaseVersion"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.FallbackToChannel, command.Option("-f|--fallbacktochannel", LanguageProvider.GetString(LanguageSection.OptionsStrings, "FallbackToChannel"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var targetEnvironmentName = GetStringFromUser(DeployOptionNames.Environment, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(DeployOptionNames.GroupFilter, LanguageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"));
            var releaseName = GetStringFromUser(DeployOptionNames.ReleaseName, LanguageProvider.GetString(LanguageSection.UiStrings, "ReleaseName"));
            var forceDefault = GetOption(DeployOptionNames.DefaultFallback).HasValue();
            var fallbackToChannel = GetStringFromUser(DeployOptionNames.FallbackToChannel, LanguageProvider.GetString(LanguageSection.UiStrings, "FallbackToChannel"));

            _progressBar.WriteStatusLine(LanguageProvider.GetString(LanguageSection.UiStrings, "CheckingOptions"));
            var targetEnvironment = await FetchEnvironmentFromUserInput(targetEnvironmentName);

            if (targetEnvironment == null)
            {
                return -2;
            }

            string fallbackChannel = null;

            if (forceDefault && !string.IsNullOrEmpty(_configuration.DefaultChannel))
            {
                fallbackChannel = _configuration.DefaultChannel;
            }

            if (!string.IsNullOrEmpty(fallbackToChannel))
            {
                fallbackChannel = fallbackToChannel;
            }

            var configResult = DeploySpecificConfig.Create(targetEnvironment, releaseName, groupRestriction, InInteractiveMode, fallbackChannel);

            if (configResult.IsFailure)
            {
                System.Console.WriteLine(configResult.Error);
                return -1;
            }
            return await _runner.Run(configResult.Value, _progressBar, InteractivePrompt, PromptForStringWithoutQuitting);
        }

        private IEnumerable<int> InteractivePrompt(DeploySpecificConfig config, (List<Project> currentProjects, List<Core.Deployment.Models.Release> targetReleases) projects)
        {
            var runner = PopulateRunner(string.Format(LanguageProvider.GetString(LanguageSection.UiStrings, "DeployingSpecificRelease"), config.ReleaseName, config.DestinationEnvironment.Name), projects.currentProjects, projects.targetReleases);
            return runner.GetSelectedIndexes();
        }

        private InteractiveRunner PopulateRunner(string prompt, IList<Project> projects, IList<Core.Deployment.Models.Release> targetReleases)
        {
            var runner = new InteractiveRunner(prompt, LanguageProvider.GetString(LanguageSection.UiStrings, "ReleaseNotSelectable"), LanguageProvider, LanguageProvider.GetString(LanguageSection.UiStrings, "ProjectName"), LanguageProvider.GetString(LanguageSection.UiStrings, "OnTarget"), LanguageProvider.GetString(LanguageSection.UiStrings, "ReleaseToDeploy"));
            foreach (var project in projects)
            {
                var release = targetReleases.FirstOrDefault(r => r.ProjectId == project.ProjectId);

                var packagesAvailable = release != null;

                runner.AddRow(project.Checked, packagesAvailable, project.ProjectName, project.CurrentRelease.Version, release?.Version);
            }
            runner.Run();
            return runner;
        }

        private struct DeployOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string DefaultFallback = "fallbacktodefault";
            public const string ReleaseName = "releasename";
            public const string FallbackToChannel = "fallbacktochannel";
        }
    }
}