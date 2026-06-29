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


using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class DeploySpecific : BaseCommand
    {
        private readonly IConfiguration _configuration;
        private readonly ICommandInteraction _interaction;
        private readonly IProgressBar _progressBar;
        private readonly DeploySpecificRunner _runner;

        public DeploySpecific(IOctopusHelper octopusHelper, IProgressBar progressBar, ILanguageProvider languageProvider, DeploySpecificRunner runner, IConfiguration configuration, ICommandInteraction interaction) : base(octopusHelper, languageProvider)
        {
            _progressBar = progressBar;
            _runner = runner;
            _configuration = configuration;
            _interaction = interaction;
        }
        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "specific";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "DeploySpecificRelease");

            AddToRegister(DeployOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.DefaultFallback, command.Option("-d|--fallbacktodefault", LanguageProvider.GetString(LanguageSection.OptionsStrings, "FallbackToDefault"), CommandOptionType.NoValue));
            AddToRegister(DeployOptionNames.ReleaseName, command.Option("-r|--releasename", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ReleaseVersion"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.FallbackToChannel, command.Option("-f|--fallbacktochannel", LanguageProvider.GetString(LanguageSection.OptionsStrings, "FallbackToChannel"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.Machine, command.Option("-m|--machine", LanguageProvider.GetString(LanguageSection.OptionsStrings, "Machine"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.Prioritise, command.Option("-p|--prioritise", LanguageProvider.GetString(LanguageSection.OptionsStrings, "Prioritise"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var targetEnvironmentName = GetStringFromUser(DeployOptionNames.Environment, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(DeployOptionNames.GroupFilter, LanguageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"));
            var releaseName = GetStringFromUser(DeployOptionNames.ReleaseName, LanguageProvider.GetString(LanguageSection.UiStrings, "ReleaseName"));
            var forceDefault = GetOption(DeployOptionNames.DefaultFallback).HasValue();
            var fallbackToChannel = GetStringFromUser(DeployOptionNames.FallbackToChannel, LanguageProvider.GetString(LanguageSection.UiStrings, "FallbackToChannel"));
            var machineName = GetStringValueFromOption(DeployOptionNames.Machine);
            var prioritise = GetBoolValueFromOption(DeployOptionNames.Prioritise);
            
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

            var machine = await FetchMachineFromUserInput(machineName, targetEnvironment);
            if (!string.IsNullOrEmpty(machineName) && machine == null)
            {
                return -2;
            }

            var configResult = DeploySpecificConfig.Create(targetEnvironment, releaseName, groupRestriction, InInteractiveMode, fallbackChannel, prioritise: prioritise, machine: machine);

            if (configResult.IsFailure)
            {
                System.Console.WriteLine(configResult.Error);
                return -1;
            }
            return await _runner.Run(configResult.Value, _progressBar, _interaction);
        }

        private struct DeployOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string DefaultFallback = "fallbacktodefault";
            public const string ReleaseName = "releasename";
            public const string FallbackToChannel = "fallbacktochannel";
            public const string Prioritise = "prioritise";
            public const string Machine = "machine";
        }
    }
}
