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
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class UpdateReleaseVariablesRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper _octopusHelper;

        public UpdateReleaseVariablesRunner(IOctopusHelper octopusHelper, ILanguageProvider languageProvider)
        {
            _octopusHelper = octopusHelper;
            _languageProvider = languageProvider;
        }

        public async Task<int> Run(ShipItSharp.Core.Deployment.Models.Environment environment, string groupFilter, bool skipConfirmation, IProgressBar progressBar, ICommandInteraction interaction)
        {
            var toUpdate = await GetProjectReleases(environment, groupFilter, progressBar);
            if (!toUpdate.Any())
            {
                return 0;
            }

            if (!skipConfirmation && !interaction.Confirm(_languageProvider.GetString(LanguageSection.UiStrings, "AreYouSureUpdateVariables"), true))
            {
                return 0;
            }

            foreach (var release in toUpdate)
            {
                System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Processing"), release.ProjectName);
                var result = await _octopusHelper.Releases.UpdateReleaseVariables(release.ReleaseId);
                System.Console.WriteLine(result ? _languageProvider.GetString(LanguageSection.UiStrings, "Done") : _languageProvider.GetString(LanguageSection.UiStrings, "Failed"), release.ProjectName);
            }

            return 0;
        }

        private async Task<List<ProjectReleaseRow>> GetProjectReleases(ShipItSharp.Core.Deployment.Models.Environment environment, string groupFilter, IProgressBar progressBar)
        {
            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(groupFilter))
            {
                progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds = (await _octopusHelper.Projects.GetFilteredProjectGroups(groupFilter)).Select(g => g.Id).ToList();
            }

            var projectStubs = await _octopusHelper.Projects.GetProjectStubs();
            var toUpdate = new List<ProjectReleaseRow>();

            progressBar.CleanCurrentLine();
            foreach (var projectStub in projectStubs)
            {
                progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(), string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                if (!string.IsNullOrEmpty(groupFilter) && !groupIds.Contains(projectStub.ProjectGroupId))
                {
                    continue;
                }

                var release = (await _octopusHelper.Releases.GetReleasedVersion(projectStub.ProjectId, environment.Id)).Release;
                if ((release != null) && !release.Version.Equals("none", StringComparison.InvariantCultureIgnoreCase))
                {
                    toUpdate.Add(new ProjectReleaseRow
                    {
                        ProjectName = projectStub.ProjectName,
                        CurrentRelease = release.Version,
                        ReleaseId = release.Id
                    });
                }
            }

            progressBar.CleanCurrentLine();
            return toUpdate;
        }
    }
}
