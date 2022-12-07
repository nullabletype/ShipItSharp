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
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class ShowEnvironment : BaseCommand
    {
        private readonly IProgressBar _progressBar;

        public ShowEnvironment(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, IProgressBar progressBar, IConfiguration configuration) : base(octopusHelper, languageProvider)
        {
            _progressBar = progressBar;
        }
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

            var found = await OctoHelper.Environments.GetEnvironment(id);
            if (found != null)
            {
                _progressBar.WriteStatusLine(LanguageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));
                var projectStubs = await OctoHelper.Projects.GetProjectStubs();

                var groupIds = new List<string>();
                if (!string.IsNullOrEmpty(groupFilter))
                {
                    _progressBar.WriteStatusLine(LanguageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                    groupIds =
                        (await OctoHelper.Projects.GetFilteredProjectGroups(groupFilter))
                        .Select(g => g.Id).ToList();
                }

                var releases = new List<(Core.Deployment.Models.Release Release, Deployment Deployment)>();

                var table = new ConsoleTable("Project", "Release Name", "Packages", "Deployed On", "Deployed By");

                foreach (var projectStub in projectStubs)
                {
                    if (!string.IsNullOrEmpty(groupFilter))
                    {
                        if (!groupIds.Contains(projectStub.ProjectGroupId))
                        {
                            continue;
                        }
                    }

                    var release = await OctoHelper.Releases.GetReleasedVersion(projectStub.ProjectId, found.Id);

                    table.AddRow(projectStub.ProjectName, release.Release.Version);
                }



            }
            System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "EnvironmentNotFound"), id);
            return -1;
        }

        private struct ShowEnvironmentOptionNames
        {
            public const string Id = "id";
            public const string GroupFilter = "groupfilter";
        }
    }
}