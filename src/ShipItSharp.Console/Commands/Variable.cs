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
using ShipItSharp.Console.Commands.SubCommands;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands
{
    internal class Variable : BaseCommand
    {

        private readonly VariablesWithProfile _variablesWithProfile;

        public Variable(IOctopusHelper octoHelper, VariablesWithProfile varsWithProfile, ILanguageProvider languageProvider) : base(octoHelper, languageProvider)
        {
            _variablesWithProfile = varsWithProfile;
        }

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "var";

        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "Variables");

            ConfigureSubCommand(_variablesWithProfile, command);
        }

        protected override Task<int> Run(CommandLineApplication command)
        {
            command.ShowHelp();
            var ts = new TaskCompletionSource<int>();
            ts.SetResult(0);
            return ts.Task;
        }
    }
}