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
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class RenameReleaseRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper _octopusHelper;

        public RenameReleaseRunner(IOctopusHelper octopusHelper, ILanguageProvider languageProvider)
        {
            _octopusHelper = octopusHelper;
            _languageProvider = languageProvider;
        }

        public async Task<int> Run(RenameReleaseConfig config, IProgressBar progressBar, ICommandInteraction interaction)
        {
            var toRename = await GetProjectReleases(config, progressBar);
            if (!toRename.Any())
            {
                return 0;
            }

            if (!interaction.Confirm(string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "GoingToRename"), config.ReleaseName), true))
            {
                return 0;
            }

            foreach (var release in toRename)
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Processing"), release.ProjectName);
                var result = await _octopusHelper.Releases.RenameRelease(release.ReleaseId, config.ReleaseName);
                if (result.success)
                {
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Done"), release.ProjectName);
                }
                else
                {
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Failed"), release.ProjectName, result.error);
                }
            }

            return 0;
        }

        private async Task<List<ProjectReleaseRow>> GetProjectReleases(RenameReleaseConfig config, IProgressBar progressBar)
        {
            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(config.GroupFilter))
            {
                progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds = (await _octopusHelper.Projects.GetFilteredProjectGroups(config.GroupFilter)).Select(g => g.Id).ToList();
            }

            var projectStubs = await _octopusHelper.Projects.GetProjectStubs();
            var toRename = new List<ProjectReleaseRow>();

            progressBar.CleanCurrentLine();
            foreach (var projectStub in projectStubs)
            {
                progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(), string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                if (!string.IsNullOrEmpty(config.GroupFilter) && !groupIds.Contains(projectStub.ProjectGroupId))
                {
                    continue;
                }

                var release = (await _octopusHelper.Releases.GetReleasedVersion(projectStub.ProjectId, config.Environment.Id)).Release;
                if ((release != null) && !release.Version.Equals("none", StringComparison.InvariantCultureIgnoreCase))
                {
                    toRename.Add(new ProjectReleaseRow
                    {
                        ProjectName = projectStub.ProjectName,
                        ReleaseId = release.Id,
                        CurrentRelease = release.Version
                    });
                }
            }

            progressBar.CleanCurrentLine();
            return toRename;
        }
    }

    public class ProjectReleaseRow
    {
        public string ProjectName { get; init; }
        public string CurrentRelease { get; init; }
        public string ReleaseId { get; init; }
    }
}
