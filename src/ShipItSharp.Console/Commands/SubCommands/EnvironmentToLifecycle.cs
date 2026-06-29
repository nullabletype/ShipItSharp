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
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class EnvironmentToLifecycle : BaseCommand
    {
        private readonly EnvironmentToLifecycleRunner _runner;

        public EnvironmentToLifecycle(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, EnvironmentToLifecycleRunner runner) : base(octopusHelper, languageProvider)
        {
            _runner = runner;
        }
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "addtolifecycle";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentToLifecycleCommand");

            AddToRegister(EnvironmentToLifecycleOptions.EnvId, command.Option("-e|--envid", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentId"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(EnvironmentToLifecycleOptions.LcId, command.Option("-l|--lcid", LanguageProvider.GetString(LanguageSection.OptionsStrings, "LifecycleId"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(EnvironmentToLifecycleOptions.PhaseId, command.Option("-p|--phasenumber", LanguageProvider.GetString(LanguageSection.OptionsStrings, "PhaseNumber"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(EnvironmentToLifecycleOptions.Automatic, command.Option("-t|--auto", LanguageProvider.GetString(LanguageSection.OptionsStrings, "AutomaticDeploy"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentId = GetStringFromUser(EnvironmentToLifecycleOptions.EnvId, string.Empty);
            var lcId = GetStringFromUser(EnvironmentToLifecycleOptions.LcId, string.Empty, true);
            var stringPhaseId = GetStringFromUser(EnvironmentToLifecycleOptions.PhaseId, string.Empty, true);
            var auto = GetOption(EnvironmentToLifecycleOptions.Automatic).HasValue();
            return await _runner.Run(environmentId, lcId, stringPhaseId, auto);
        }

        private struct EnvironmentToLifecycleOptions
        {
            public const string EnvId = "envid";
            public const string LcId = "lcid";
            public const string PhaseId = "phaseid";
            public const string Automatic = "automatic";
        }
    }
}
