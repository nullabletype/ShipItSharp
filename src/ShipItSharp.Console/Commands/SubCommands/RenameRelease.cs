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
using NuGet.Versioning;
using ShipItSharp.Console.ConsoleTools;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Console.Commands.SubCommands
{
    internal class RenameRelease : BaseCommand
    {
        //todo convert to runner
        private readonly IProgressBar _progressBar;

        public RenameRelease(IOctopusHelper octopusHelper, IProgressBar progressBar, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider)
        {
            _progressBar = progressBar;
        }

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "rename";


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(RenameReleaseOptionNames.Environment, command.Option("-e|--environment", LanguageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(RenameReleaseOptionNames.ReleaseName, command.Option("-r|--releasename", LanguageProvider.GetString(LanguageSection.OptionsStrings, "ReleaseVersion"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(RenameReleaseOptionNames.GroupFilter, command.Option("-g|--groupfilter", LanguageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentName = GetStringFromUser(RenameReleaseOptionNames.Environment, LanguageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var releaseName = GetStringFromUser(RenameReleaseOptionNames.ReleaseName, LanguageProvider.GetString(LanguageSection.UiStrings, "ReleaseNamePrompt"));
            var groupRestriction = GetStringFromUser(RenameReleaseOptionNames.GroupFilter, LanguageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"), true);

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }

            if (!SemanticVersion.TryParse(releaseName, out _))
            {
                System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "InvalidReleaseVersion"));
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

            var toRename = new List<ProjectRelease>();

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
                    toRename.Add(new ProjectRelease { Release = release, ProjectStub = projectStub });
                }
            }
            _progressBar.StopAnimation();
            _progressBar.CleanCurrentLine();

            System.Console.WriteLine();

            var table = new ConsoleTable(LanguageProvider.GetString(LanguageSection.UiStrings, "ProjectName"), LanguageProvider.GetString(LanguageSection.UiStrings, "CurrentRelease"));
            foreach (var release in toRename)
            {
                table.AddRow(release.ProjectStub.ProjectName, release.Release.Version);
            }

            table.Write(Format.Minimal);

            if (Prompt.GetYesNo(string.Format(LanguageProvider.GetString(LanguageSection.UiStrings, "GoingToRename"), releaseName), true))
            {
                foreach (var release in toRename)
                {
                    System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "Processing"), release.ProjectStub.ProjectName);
                    var result = await OctoHelper.Releases.RenameRelease(release.Release.Id, releaseName);
                    if (result.success)
                    {
                        System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "Done"), release.ProjectStub.ProjectName);
                    }
                    else
                    {
                        System.Console.WriteLine(LanguageProvider.GetString(LanguageSection.UiStrings, "Failed"), release.ProjectStub.ProjectName, result.error);
                    }
                }
            }

            return 0;
        }

        private struct RenameReleaseOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string ReleaseName = "releasename";
        }

        private class ProjectRelease
        {
            public ProjectStub ProjectStub { get; init; }
            public Core.Deployment.Models.Release Release { get; init; }
        }
    }
}