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


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    class DeploySpecific : BaseCommand
    {
        private IProgressBar progressBar;
        private DeploySpecificRunner runner;
        private IConfiguration configuration;
        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "specific";

        public DeploySpecific(IOctopusHelper octopusHelper, IProgressBar progressBar, ILanguageProvider languageProvider, DeploySpecificRunner runner, IConfiguration configuration) : base(octopusHelper, languageProvider) 
        {
            this.progressBar = progressBar;
            this.runner = runner;
            this.configuration = configuration;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(DeployOptionNames.Environment, command.Option("-e|--environment", languageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.GroupFilter, command.Option("-g|--groupfilter", languageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.DefaultFallback, command.Option("-d|--fallbacktodefault", languageProvider.GetString(LanguageSection.OptionsStrings, "FallbackToDefault"), CommandOptionType.NoValue));
            AddToRegister(DeployOptionNames.ReleaseName, command.Option("-r|--releasename", languageProvider.GetString(LanguageSection.OptionsStrings, "ReleaseVersion"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.FallbackToChannel, command.Option("-f|--fallbacktochannel", languageProvider.GetString(LanguageSection.OptionsStrings, "FallbackToChannel"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var targetEnvironmentName = GetStringFromUser(DeployOptionNames.Environment, languageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(DeployOptionNames.GroupFilter, languageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"));
            var releaseName = GetStringFromUser(DeployOptionNames.ReleaseName, languageProvider.GetString(LanguageSection.UiStrings, "ReleaseName"));
            var forceDefault = GetOption(DeployOptionNames.DefaultFallback).HasValue();
            var fallbackToChannel = GetStringFromUser(DeployOptionNames.FallbackToChannel, languageProvider.GetString(LanguageSection.UiStrings, "FallbackToChannel"));

            progressBar.WriteStatusLine(languageProvider.GetString(LanguageSection.UiStrings, "CheckingOptions"));
            var targetEnvironment = await FetchEnvironmentFromUserInput(targetEnvironmentName);

            if (targetEnvironment == null)
            {
                return -2;
            }

            string fallbackChannel = null;

            if (forceDefault && !string.IsNullOrEmpty(configuration.DefaultChannel))
            {
                fallbackChannel = configuration.DefaultChannel;
            }

            if (!string.IsNullOrEmpty(fallbackToChannel))
            {
                fallbackChannel = fallbackToChannel;
            }

            var configResult = DeploySpecificConfig.Create(targetEnvironment, releaseName, groupRestriction, this.InInteractiveMode, fallbackChannel);

            if (configResult.IsFailure)
            {
                System.Console.WriteLine(configResult.Error);
                return -1;
            }
            else
            {
                return await runner.Run(configResult.Value, this.progressBar, InteractivePrompt, PromptForStringWithoutQuitting);
            }
        }

        private IEnumerable<int> InteractivePrompt(DeploySpecificConfig config, (List<Project> currentProjects, List<Core.Models.Release> targetReleases) projects)
        {
            InteractiveRunner runner = PopulateRunner(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "DeployingSpecificRelease"), config.ReleaseName, config.DestinationEnvironment.Name), projects.currentProjects, projects.targetReleases);
            return runner.GetSelectedIndexes();
        }

        private InteractiveRunner PopulateRunner(string prompt, IList<Project> projects, IList<Core.Models.Release> targetReleases)
        {
            var runner = new InteractiveRunner(prompt, languageProvider.GetString(LanguageSection.UiStrings, "ReleaseNotSelectable"), languageProvider, languageProvider.GetString(LanguageSection.UiStrings, "ProjectName"), languageProvider.GetString(LanguageSection.UiStrings, "OnTarget"), languageProvider.GetString(LanguageSection.UiStrings, "ReleaseToDeploy"));
            foreach (var project in projects)
            {
                var release = targetReleases.FirstOrDefault(r => r.ProjectId == project.ProjectId);

                var packagesAvailable = release != null;

                runner.AddRow(project.Checked, packagesAvailable, new[] {
                        project.ProjectName,
                        project.CurrentRelease.Version,
                        release?.Version
                    });
            }
            runner.Run();
            return runner;
        }

        struct DeployOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string DefaultFallback = "fallbacktodefault";
            public const string ReleaseName = "releasename";
            public const string FallbackToChannel = "fallbacktochannel";
        }
    }
}
