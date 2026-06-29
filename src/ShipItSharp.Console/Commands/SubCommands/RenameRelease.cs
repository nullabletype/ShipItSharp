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
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class RenameRelease : BaseCommand
    {
        private readonly ICommandInteraction _interaction;
        private readonly IProgressBar _progressBar;
        private readonly RenameReleaseRunner _runner;

        public RenameRelease(IOctopusHelper octopusHelper, IProgressBar progressBar, ILanguageProvider languageProvider, RenameReleaseRunner runner, ICommandInteraction interaction) : base(octopusHelper, languageProvider)
        {
            _progressBar = progressBar;
            _runner = runner;
            _interaction = interaction;
        }

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "rename";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "RenameReleaseCommand");

            AddToRegister(RenameReleaseOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(RenameReleaseOptionNames.ReleaseName, command.Option("-r|--releasename", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ReleaseVersion"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(RenameReleaseOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentName = GetStringFromUser(RenameReleaseOptionNames.Environment, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var releaseName = GetStringFromUser(RenameReleaseOptionNames.ReleaseName, LanguageProvider.GetString(LanguageSection.UiStrings, "ReleaseNamePrompt"));
            var groupRestriction = GetStringFromUser(RenameReleaseOptionNames.GroupFilter, LanguageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"), true);

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }

            var configResult = RenameReleaseConfig.Create(groupRestriction, environment, InInteractiveMode, releaseName);
            if (configResult.IsFailure)
            {
                System.Console.WriteLine(configResult.Error);
                return -1;
            }

            return await _runner.Run(configResult.Value, _progressBar, _interaction);
        }

        private struct RenameReleaseOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string ReleaseName = "releasename";
        }
    }
}
