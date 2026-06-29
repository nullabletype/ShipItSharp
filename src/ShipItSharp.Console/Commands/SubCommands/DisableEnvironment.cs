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
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class DisableEnvironment : BaseCommand
    {
        private readonly DisableEnvironmentRunner _runner;

        public DisableEnvironment(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, DisableEnvironmentRunner runner) : base(octopusHelper, languageProvider)
        {
            _runner = runner;
        }

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "disable";

        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "DisableEnvironmentCommand");

            AddToRegister(DisableEnvironmentOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(DisableEnvironmentOptionNames.Machine, command.Option("-m|--machine", LanguageProvider.GetString(LanguageSection.OptionsStrings, "Machine"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentInput = GetStringFromUser(DisableEnvironmentOptionNames.Environment, string.Empty);
            var machineInput = GetStringValueFromOption(DisableEnvironmentOptionNames.Machine);

            var environment = await OctoHelper.Environments.GetEnvironment(environmentInput) ?? await FetchEnvironmentFromUserInput(environmentInput);
            if (environment == null)
            {
                return -2;
            }

            var machine = await FetchMachineFromUserInput(machineInput, environment);
            if (!string.IsNullOrEmpty(machineInput) && machine == null)
            {
                return -2;
            }

            return await _runner.Run(environment, machine);
        }

        private struct DisableEnvironmentOptionNames
        {
            public const string Environment = "environment";
            public const string Machine = "machine";
        }
    }
}
