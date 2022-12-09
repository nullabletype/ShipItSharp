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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class UpdateReleaseVariables : BaseCommand
    {
        //todo convert to runner
        private readonly IProgressBar _progressBar;

        public UpdateReleaseVariables(IOctopusHelper octopusHelper, IProgressBar progressBar, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider)
        {
            _progressBar = progressBar;
        }

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "updatevariables";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(UpdateReleaseVariablesOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(UpdateReleaseVariablesOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(UpdateReleaseVariablesOptionNames.SkipConfirmation, command.Option("-s|--skipconfirmation", LanguageProvider.GetString(LanguageSection.OptionsStrings, "SkipConfirmation"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentName = GetStringFromUser(UpdateReleaseVariablesOptionNames.Environment, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(UpdateReleaseVariablesOptionNames.GroupFilter, LanguageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"), true);

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(groupRestriction))
            {
                _progressBar.WriteStatusLine(LanguageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds =
                    (await OctoHelper.Projects.GetFilteredProjectGroups(groupRestriction))
                    .Select(g => g.Id).ToList();
            }

            var projectStubs = await OctoHelper.Projects.GetProjectStubs();

            var toUpdate = new List<ProjectRelease>();

            _progressBar.CleanCurrentLine();

            foreach (var projectStub in projectStubs)
            {
                _progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
                    string.Format(LanguageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                if (!string.IsNullOrEmpty(groupRestriction))
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId))
                    {
                        continue;
                    }
                }

                var release = (await OctoHelper.Releases.GetReleasedVersion(projectStub.ProjectId, environment.Id)).Release;
                if ((release != null) && !release.Version.Equals("none", StringComparison.InvariantCultureIgnoreCase))
                {
                    toUpdate.Add(new ProjectRelease { Release = release, ProjectStub = projectStub });
                }
            }

            _progressBar.StopAnimation();
            _progressBar.CleanCurrentLine();

            System.Console.WriteLine();

            var table = new ConsoleTable(LanguageProvider.GetString(LanguageSection.UiStrings, "ProjectName"), LanguageProvider.GetString(LanguageSection.UiStrings, "CurrentRelease"));
            foreach (var release in toUpdate)
            {
                table.AddRow(release.ProjectStub.ProjectName, release.Release.Version);
            }

            table.Write(Format.Minimal);

            if (Prompt.GetYesNo(LanguageProvider.GetString(LanguageSection.UiStrings, "AreYouSureUpdateVariables"), true))
            {
                foreach (var release in toUpdate)
                {
                    System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "Processing"), release.ProjectStub.ProjectName);
                    var result = await OctoHelper.Releases.UpdateReleaseVariables(release.Release.Id);
                    System.Console.WriteLine(result ? LanguageProvider.GetString(LanguageSection.UiStrings, "Done") : LanguageProvider.GetString(LanguageSection.UiStrings, "Failed"), release.ProjectStub.ProjectName);
                }
            }

            return 0;
        }

        private struct UpdateReleaseVariablesOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string SkipConfirmation = "skipconfirmation";
        }

        private class ProjectRelease
        {
            public ProjectStub ProjectStub { get; set; }
            public Core.Deployment.Models.Release Release { get; set; }
        }
    }
}