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
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.JobRunners
{
    public class ChannelsRunner
    {
        private readonly ILanguageProvider _languageProvider;
        private readonly IOctopusHelper _octopusHelper;
        private readonly IProgressBar _progressBar;
        private readonly IUiLogger _uiLogger;

        public ChannelsRunner(IProgressBar progressBar, IOctopusHelper octopusHelper, ILanguageProvider languageProvider, IUiLogger uiLogger)
        {
            _octopusHelper = octopusHelper;
            _progressBar = progressBar;
            _languageProvider = languageProvider;
            _uiLogger = uiLogger;
        }

        public async Task<bool> Cleanup(ChannelCleanupConfig config)
        {
            var toDelete = new List<(string ProjectId, string ProjectName, string ChannelId, string ChannelName)>();

            if (!string.IsNullOrEmpty(config.GroupFilter))
            {
                var groupIds = (await _octopusHelper.Projects.GetFilteredProjectGroups(config.GroupFilter)).Select(g => g.Id);
                var projectStubs = await _octopusHelper.Projects.GetProjectStubs();

                foreach (var projectStub in projectStubs)
                {
                    _progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
                        string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                    if (!string.IsNullOrEmpty(config.GroupFilter))
                    {
                        if (!groupIds.Contains(projectStub.ProjectGroupId))
                        {
                            continue;
                        }
                    }

                    var channels = await _octopusHelper.Channels.GetChannelsForProject(projectStub.ProjectId, 9999);
                    var packageSteps = await _octopusHelper.Packages.GetPackages(projectStub.ProjectId, null, null, 9999);
                    var packages = packageSteps.SelectMany(p => p.AvailablePackages);

                    foreach (var channel in channels)
                    {
                        if (!packages.Any(p => channel.ValidateVersion(p.Version)))
                        {
                            toDelete.Add((projectStub.ProjectId, projectStub.ProjectName, channel.Id, channel.Name));
                        }
                    }
                }
            }

            var failed = new List<(string ProjectName, string ChannelName, IEnumerable<Release> Releases)>();

            foreach (var current in toDelete)
            {
                var message = "";
                if (config.TestMode)
                {
                    message += _languageProvider.GetString(LanguageSection.UiStrings, "Test") + " ";
                }
                message += string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "RemovingChannel"), current.ChannelName, current.ProjectName);

                _uiLogger.WriteLine(message);

                if (!config.TestMode)
                {
                    var result = await _octopusHelper.Channels.RemoveChannel(current.ChannelId);
                    if (!result.Success)
                    {
                        failed.Add((current.ProjectName, current.ChannelName, result.Releases));
                    }
                }

            }

            foreach (var current in failed)
            {
                _uiLogger.WriteLine(string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "CouldntRemoveChannelReleases"), current.ChannelName, current.ProjectName, string.Join(',', current.Releases.Select(r => r.Version))));
            }

            return !failed.Any();
        }
    }
}