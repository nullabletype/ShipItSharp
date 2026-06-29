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


using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class EnsureEnvironment : BaseCommand
    {
        private readonly EnsureEnvironmentRunner _runner;

        public EnsureEnvironment(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, EnsureEnvironmentRunner runner) : base(octopusHelper, languageProvider)
        {
            _runner = runner;
        }
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "ensure";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnsureEnvironmentCommand");

            AddToRegister(EnsureEnvironmentOptionNames.Name, command.Option("-n|--name", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(EnsureEnvironmentOptionNames.Description, command.Option("-d|--description", LanguageProvider.GetString(LanguageSection.OptionsStrings, "Description"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var name = GetStringFromUser(EnsureEnvironmentOptionNames.Name, string.Empty);
            var description = GetStringFromUser(EnsureEnvironmentOptionNames.Description, string.Empty, true);
            return await _runner.Run(name, description);
        }

        private struct EnsureEnvironmentOptionNames
        {
            public const string Name = "name";
            public const string Description = "description";
        }
    }
}
