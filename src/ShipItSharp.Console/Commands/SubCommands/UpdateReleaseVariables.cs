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
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class UpdateReleaseVariables : BaseCommand
    {
        private readonly ICommandInteraction _interaction;
        private readonly IProgressBar _progressBar;
        private readonly UpdateReleaseVariablesRunner _runner;

        public UpdateReleaseVariables(IOctopusHelper octopusHelper, IProgressBar progressBar, ILanguageProvider languageProvider, UpdateReleaseVariablesRunner runner, ICommandInteraction interaction) : base(octopusHelper, languageProvider)
        {
            _progressBar = progressBar;
            _runner = runner;
            _interaction = interaction;
        }

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "updatevariables";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "UpdateReleaseVariablesCommand");

            AddToRegister(UpdateReleaseVariablesOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(UpdateReleaseVariablesOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(UpdateReleaseVariablesOptionNames.SkipConfirmation, command.Option("-s|--skipconfirmation", LanguageProvider.GetString(LanguageSection.OptionsStrings, "SkipConfirmation"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentName = GetStringFromUser(UpdateReleaseVariablesOptionNames.Environment, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(UpdateReleaseVariablesOptionNames.GroupFilter, LanguageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"), true);
            var skipConfirmation = GetOption(UpdateReleaseVariablesOptionNames.SkipConfirmation).HasValue();

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }

            return await _runner.Run(environment, groupRestriction, skipConfirmation, _progressBar, _interaction);
        }

        private struct UpdateReleaseVariablesOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string SkipConfirmation = "skipconfirmation";
        }
    }
}
