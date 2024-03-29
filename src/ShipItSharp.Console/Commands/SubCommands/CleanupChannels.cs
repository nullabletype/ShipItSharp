﻿#region copyright
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
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class CleanupChannels : BaseCommand
    {
        private readonly ChannelsRunner _runner;

        public CleanupChannels(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, ChannelsRunner runner) : base(octopusHelper, languageProvider)
        {
            _runner = runner;
        }
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "cleanup";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(EnsureEnvironmentOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(EnsureEnvironmentOptionNames.TestMode, command.Option("-t|--testmode", LanguageProvider.GetString(LanguageSection.OptionsStrings, "TestMode"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var groupFilter = GetStringFromUser(EnsureEnvironmentOptionNames.GroupFilter, string.Empty);
            var testMode = GetBoolValueFromOption(EnsureEnvironmentOptionNames.TestMode);

            var config = ChannelCleanupConfig.Create(groupFilter, testMode);

            if (config.IsSuccess)
            {
                await _runner.Cleanup(config.Value);
            }

            return 0;
        }

        private struct EnsureEnvironmentOptionNames
        {
            public const string GroupFilter = "groupfilter";
            public const string TestMode = "testmode";
        }
    }
}