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
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands
{
    internal class Promote : BaseCommand
    {
        private readonly ICommandInteraction _interaction;
        private readonly IProgressBar _progressBar;
        private readonly PromotionRunner _runner;

        public Promote(IOctopusHelper octoHelper, IProgressBar progressBar, ILanguageProvider languageProvider, PromotionRunner runner, ICommandInteraction interaction) : base(octoHelper, languageProvider)
        {
            _progressBar = progressBar;
            _runner = runner;
            _interaction = interaction;
        }

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "promote";

        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "PromoteProjects");

            AddToRegister(PromoteOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue));
            AddToRegister(PromoteOptionNames.SourceEnvironment, command.Option("-s|--sourcenvironment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "SourceEnvironment"), CommandOptionType.SingleValue));
            AddToRegister(PromoteOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(PromoteOptionNames.Machine, command.Option("-m|--machine", LanguageProvider.GetString(LanguageSection.OptionsStrings, "Machine"), CommandOptionType.SingleValue));
            AddToRegister(PromoteOptionNames.Prioritise, command.Option("-p|--prioritise", LanguageProvider.GetString(LanguageSection.OptionsStrings, "Prioritise"), CommandOptionType.NoValue));
            AddToRegister(PromoteOptionNames.UpdateVariables, command.Option("-v|--updatevariables", LanguageProvider.GetString(LanguageSection.OptionsStrings, "UpdateVariables"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            _progressBar.WriteStatusLine(LanguageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));

            var environmentName = GetStringFromUser(PromoteOptionNames.SourceEnvironment, LanguageProvider.GetString(LanguageSection.UiStrings, "SourceEnvironment"));
            var targetEnvironmentName = GetStringFromUser(PromoteOptionNames.Environment, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(PromoteOptionNames.GroupFilter, LanguageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"));
            var machineName = GetStringValueFromOption(PromoteOptionNames.Machine);
            var prioritise = GetBoolValueFromOption(PromoteOptionNames.Prioritise);
            var updateVariables = GetBoolValueFromOption(PromoteOptionNames.UpdateVariables);
            
            _progressBar.WriteStatusLine(LanguageProvider.GetString(LanguageSection.UiStrings, "CheckingOptions"));
            var environment = await FetchEnvironmentFromUserInput(environmentName);
            var targetEnvironment = await FetchEnvironmentFromUserInput(targetEnvironmentName);

            if (environment == null || targetEnvironment == null)
            {
                return -2;
            }

            var machine = await FetchMachineFromUserInput(machineName, targetEnvironment);
            if (!string.IsNullOrEmpty(machineName) && machine == null)
            {
                return -2;
            }

            var configResult = PromotionConfig.Create(targetEnvironment, environment, groupRestriction, InInteractiveMode, prioritise: prioritise, updateVariables: updateVariables, machine: machine);

            if (configResult.IsFailure)
            {
                System.Console.WriteLine(configResult.Error);
                return -1;
            }
            return await _runner.Run(configResult.Value, _progressBar, _interaction);
        }

        private struct PromoteOptionNames
        {
            public const string SourceEnvironment = "sourceenvironment";
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string Prioritise = "prioritise";
            public const string UpdateVariables = "updatevariables";
            public const string Machine = "machine";
        }
    }

}
