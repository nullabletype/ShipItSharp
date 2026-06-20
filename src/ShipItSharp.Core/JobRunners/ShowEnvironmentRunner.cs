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
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class ShowEnvironmentRunner
    {
        private readonly IOctopusHelper _octopusHelper;

        public ShowEnvironmentRunner(IOctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public async Task<(bool Found, List<ShowEnvironmentRow> Rows)> Run(string id, string groupFilter, IProgressBar progressBar, string fetchingMessage, string groupsMessage, string loadingMessageTemplate)
        {
            var found = await _octopusHelper.Environments.GetEnvironment(id);
            if (found == null)
            {
                return (false, new List<ShowEnvironmentRow>());
            }

            progressBar.WriteStatusLine(fetchingMessage);
            var projectStubs = await _octopusHelper.Projects.GetProjectStubs();

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(groupFilter))
            {
                progressBar.WriteStatusLine(groupsMessage);
                groupIds =
                    (await _octopusHelper.Projects.GetFilteredProjectGroups(groupFilter))
                    .Select(g => g.Id).ToList();
            }

            var rows = new List<ShowEnvironmentRow>();
            foreach (var projectStub in projectStubs)
            {
                progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(), string.Format(loadingMessageTemplate, projectStub.ProjectName));

                if (!string.IsNullOrEmpty(groupFilter) && !groupIds.Contains(projectStub.ProjectGroupId))
                {
                    continue;
                }

                var release = await _octopusHelper.Releases.GetReleasedVersion(projectStub.ProjectId, found.Id);
                if (release.Release == null)
                {
                    continue;
                }

                rows.Add(new ShowEnvironmentRow
                {
                    ProjectName = projectStub.ProjectName,
                    ReleaseName = release.Release.Version,
                    DeployedBy = release.Release.LastModifiedBy,
                    DeployedOn = release.Release.LastModifiedOn?.ToString("u"),
                    Packages = release.Release.DisplayPackageVersion
                });
            }

            progressBar.CleanCurrentLine();
            return (true, rows);
        }
    }

    public class ShowEnvironmentRow
    {
        public string ProjectName { get; init; }
        public string ReleaseName { get; init; }
        public string Packages { get; init; }
        public string DeployedOn { get; init; }
        public string DeployedBy { get; init; }
    }
}
