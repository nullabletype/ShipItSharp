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
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class DeleteEnvironment : BaseCommand
    {
        private readonly ICommandInteraction _interaction;
        private readonly DeleteEnvironmentRunner _runner;

        public DeleteEnvironment(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, DeleteEnvironmentRunner runner, ICommandInteraction interaction) : base(octopusHelper, languageProvider)
        {
            _runner = runner;
            _interaction = interaction;
        }
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "delete";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(EnsureEnvironmentOptionNames.Id, command.Option("-e|--e", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(EnsureEnvironmentOptionNames.SkipConfirmation, command.Option("-s|--skipconfirmation", LanguageProvider.GetString(LanguageSection.OptionsStrings, "SkipConfirmation"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var id = GetStringFromUser(EnsureEnvironmentOptionNames.Id, string.Empty);
            var skipConfirm = GetOption(EnsureEnvironmentOptionNames.SkipConfirmation).HasValue();
            return await _runner.Run(id, skipConfirm, _interaction);
        }

        private struct EnsureEnvironmentOptionNames
        {
            public const string Id = "id";
            public const string SkipConfirmation = "skipconfirmation";
        }
    }
}
