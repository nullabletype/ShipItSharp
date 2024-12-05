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
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Console.Commands.SubCommands;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands
{
    internal class Deploy : BaseCommand
    {
        private readonly IConfiguration _configuration;
        private readonly DeploySpecific _deploySpecific;
        private readonly DeployWithProfile _profile;
        private readonly DeployWithProfileDirectory _profileDir;
        private readonly IProgressBar _progressBar;
        private readonly DeployRunner _runner;

        public Deploy(DeployRunner deployRunner, IConfiguration configuration, IOctopusHelper octoHelper, DeployWithProfile profile, DeployWithProfileDirectory profileDir, DeploySpecific deploySpecific, IProgressBar progressBar, ILanguageProvider languageProvider) : base(octoHelper, languageProvider)
        {
            _configuration = configuration;
            _profile = profile;
            _profileDir = profileDir;
            _progressBar = progressBar;
            _runner = deployRunner;
            _deploySpecific = deploySpecific;
        }

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "deploy";

        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "DeployProjects");

            ConfigureSubCommand(_profile, command);
            ConfigureSubCommand(_profileDir, command);
            ConfigureSubCommand(_deploySpecific, command);

            AddToRegister(DeployOptionNames.ChannelName, command.Option("-c|--channel", LanguageProvider.GetString(LanguageSection.OptionsStrings, "DeployChannel"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.SaveProfile, command.Option("-s|--saveprofile", LanguageProvider.GetString(LanguageSection.OptionsStrings, "SaveProfile"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.DefaultFallback, command.Option("-d|--fallbacktodefault", LanguageProvider.GetString(LanguageSection.OptionsStrings, "FallbackToDefault"), CommandOptionType.NoValue));
            AddToRegister(OptionNames.ReleaseName, command.Option("-r|--releasename", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ReleaseVersion"), CommandOptionType.SingleValue));
        }


        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetStringValueFromOption(DeployOptionNames.SaveProfile);
            if (!string.IsNullOrEmpty(profilePath))
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "GoingToSaveProfile"), profilePath);
            }
            _progressBar.WriteStatusLine(LanguageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));
            var projectStubs = await OctoHelper.Projects.GetProjectStubs();

            var channelName = GetStringFromUser(DeployOptionNames.ChannelName, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichChannelPrompt"));
            var environmentName = GetStringFromUser(DeployOptionNames.Environment, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(DeployOptionNames.GroupFilter, LanguageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"), true);
            var forceDefault = GetOption(DeployOptionNames.DefaultFallback).HasValue();

            _progressBar.WriteStatusLine(LanguageProvider.GetString(LanguageSection.UiStrings, "CheckingOptions"));

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }
            
            Log.Info($"Found environment for {environmentName} with id {environment.Id}");

            var configResult = DeployConfig.Create(environment, channelName, forceDefault ? _configuration.DefaultChannel : null, groupRestriction, GetStringValueFromOption(DeployOptionNames.SaveProfile), InInteractiveMode);

            if (configResult.IsFailure)
            {
                System.Console.WriteLine(configResult.Error);
                return -1;
            }
            return await _runner.Run(configResult.Value, _progressBar, projectStubs, InteractivePrompt, PromptForStringWithoutQuitting, text => { return Prompt.GetString(text); });
        }

        private IEnumerable<int> InteractivePrompt(DeployConfig config, IList<Project> projects)
        {
            var runner = PopulateRunner(string.Format(LanguageProvider.GetString(LanguageSection.UiStrings, "DeployingTo"), config.Channel, config.Environment.Name), LanguageProvider.GetString(LanguageSection.UiStrings, "PackageNotSelectable"), projects);
            return runner.GetSelectedIndexes();
        }


        private InteractiveRunner PopulateRunner(string prompt, string unselectableText, IEnumerable<Project> projects)
        {
            var runner = new InteractiveRunner(prompt,
                unselectableText,
                LanguageProvider,
                LanguageProvider.GetString(LanguageSection.UiStrings, "ProjectName"),
                LanguageProvider.GetString(LanguageSection.UiStrings, "CurrentRelease"),
                LanguageProvider.GetString(LanguageSection.UiStrings, "CurrentPackage"),
                LanguageProvider.GetString(LanguageSection.UiStrings, "NewPackage"),
                LanguageProvider.GetString(LanguageSection.UiStrings, "OldestPackagePublish"),
                LanguageProvider.GetString(LanguageSection.UiStrings, "PackageAgeDays")
            );

            foreach (var project in projects)
            {
                var packagesAvailable = (project.AvailablePackages.Count > 0) && project.AvailablePackages.All(p => p.SelectedPackage != null);

                DateTime? lastModified = null;

                foreach (var package in project.AvailablePackages)
                {
                    if (((lastModified == null) && (package.SelectedPackage != null) && package.SelectedPackage.PublishedOn.HasValue) || ((package.SelectedPackage != null) && package.SelectedPackage.PublishedOn.HasValue && (package.SelectedPackage.PublishedOn < lastModified)))
                    {
                        lastModified = package.SelectedPackage.PublishedOn;
                    }
                }

                runner.AddRow(project.Checked, packagesAvailable, project.ProjectName, project.CurrentRelease.Version, project.AvailablePackages.Count > 1 ? LanguageProvider.GetString(LanguageSection.UiStrings, "Multi") : project.CurrentRelease.DisplayPackageVersion, project.AvailablePackages.Count > 1 ? LanguageProvider.GetString(LanguageSection.UiStrings, "Multi") :
                    packagesAvailable ? project.AvailablePackages.First().SelectedPackage.Version : string.Empty, lastModified.HasValue ? $"{lastModified.Value.ToShortDateString()} : {lastModified.Value.ToShortTimeString()}" : string.Empty,
                    lastModified.HasValue ? $"{DateTime.Now.Subtract(lastModified.Value).Days.ToString()}{(lastModified.Value < DateTime.Now.AddDays(-7) ? "*" : string.Empty)}" : string.Empty);

            }
            runner.Run();
            return runner;
        }

        private struct DeployOptionNames
        {
            public const string ChannelName = "channel";
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string SaveProfile = "saveprofile";
            public const string DefaultFallback = "fallbacktodefault";
        }
    }
}