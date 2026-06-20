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


using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class ShowEnvironment(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, IProgressBar progressBar, ShowEnvironmentRunner runner)
        : BaseCommand(octopusHelper, languageProvider)
    {

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "show";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(ShowEnvironmentOptionNames.Id, command.Option("-e|--e", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(ShowEnvironmentOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var id = GetStringFromUser(ShowEnvironmentOptionNames.Id, string.Empty);
            var groupFilter = GetStringFromUser(ShowEnvironmentOptionNames.GroupFilter, string.Empty, true);

            var result = await runner.Run(
                id,
                groupFilter,
                progressBar,
                LanguageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"),
                LanguageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"),
                LanguageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"));

            if (!result.Found)
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "EnvironmentNotFound"), id);
                return -1;
            }

            var table = new ConsoleTable("Project", "Release Name", "Packages", "Deployed On", "Deployed By");
            foreach (var row in result.Rows)
            {
                table.AddRow(row.ProjectName, row.ReleaseName, row.Packages, row.DeployedOn, row.DeployedBy);
            }

            table.Write(Format.Minimal);
            return 0;
        }

        private struct ShowEnvironmentOptionNames
        {
            public const string Id = "id";
            public const string GroupFilter = "groupfilter";
        }
    }
}
