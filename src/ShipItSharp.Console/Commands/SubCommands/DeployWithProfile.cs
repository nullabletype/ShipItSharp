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
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class DeployWithProfile : BaseCommand
    {
        private readonly IJobRunner _consoleDoJob;

        public DeployWithProfile(IJobRunner consoleDoJob, IOctopusHelper octopusHelper, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider)
        {
            _consoleDoJob = consoleDoJob;
        }

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profile";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(DeployWithProfileOptionNames.File, command.Option("-f|--file", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ProfileFile"), CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister(DeployWithProfileOptionNames.ForceRedeploy, command.Option("-r|--forceredeploy", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ForceDeployOfSamePackage"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetOption(DeployWithProfileOptionNames.File).Value();
            System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "UsingProfileAtPath") + profilePath);
            await _consoleDoJob.StartJob(profilePath, null, null, GetOption(DeployWithProfileOptionNames.ForceRedeploy).HasValue());
            return 0;
        }

        private struct DeployWithProfileOptionNames
        {
            public const string File = "file";
            public const string ForceRedeploy = "forceredeploy";
        }
    }
}