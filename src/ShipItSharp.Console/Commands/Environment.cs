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
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands
{
    internal class Environment : BaseCommand
    {
        private readonly DeleteEnvironment _delEnv;
        private readonly DisableEnvironment _disableEnv;
        private readonly EnableEnvironment _enableEnv;
        private readonly EnvironmentRunner _environmentRunner;
        private readonly EnsureEnvironment _ensureEnv;
        private readonly EnvironmentToLifecycle _envToLifecycle;
        private readonly EnvironmentToTeam _envToTeam;
        private readonly ShowEnvironment _showEnv;

        public Environment(IOctopusHelper octoHelper, EnsureEnvironment ensureEnv, DeleteEnvironment delEnv, DisableEnvironment disableEnv, EnableEnvironment enableEnv, EnvironmentToTeam envToTeam, EnvironmentToLifecycle envToLifecycle, ShowEnvironment showEnv, ILanguageProvider languageProvider, EnvironmentRunner environmentRunner) : base(octoHelper, languageProvider)
        {
            _ensureEnv = ensureEnv;
            _delEnv = delEnv;
            _disableEnv = disableEnv;
            _enableEnv = enableEnv;
            _envToTeam = envToTeam;
            _envToLifecycle = envToLifecycle;
            _showEnv = showEnv;
            _environmentRunner = environmentRunner;
        }

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "env";

        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            ConfigureSubCommand(_ensureEnv, command);
            ConfigureSubCommand(_delEnv, command);
            ConfigureSubCommand(_disableEnv, command);
            ConfigureSubCommand(_enableEnv, command);
            ConfigureSubCommand(_envToTeam, command);
            ConfigureSubCommand(_envToLifecycle, command);
            ConfigureSubCommand(_showEnv, command);

            command.Description = LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentCommands");
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var envs = await _environmentRunner.GetEnvironments();
            var table = new ConsoleTable(LanguageProvider.GetString(LanguageSection.UiStrings, "Name"), LanguageProvider.GetString(LanguageSection.UiStrings, "Id"));
            foreach (var env in envs)
            {
                table.AddRow(env.Name, env.Id);
            }

            table.Write(Format.Minimal);
            return 0;
        }
    }
}
