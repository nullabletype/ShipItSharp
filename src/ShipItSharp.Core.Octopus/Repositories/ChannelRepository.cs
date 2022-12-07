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
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories
{
    public class ChannelRepository : IChannelRepository
    {
        private readonly OctopusHelper _octopusHelper;

        public ChannelRepository(OctopusHelper octopusHelper)
        {
            _octopusHelper = octopusHelper;
        }

        public async Task<Channel> GetChannelByProjectNameAndChannelName(string name, string channelName)
        {
            var project = await _octopusHelper.Client.Repository.Projects.FindOne(resource => resource.Name == name, CancellationToken.None);
            return ConvertChannel(await _octopusHelper.Client.Repository.Channels.FindByName(project, channelName));
        }

        public async Task<Channel> GetChannelByName(string projectIdOrName, string channelName)
        {
            var project = await _octopusHelper.Projects.GetProject(projectIdOrName);
            return ConvertChannel(await _octopusHelper.Client.Repository.Channels.FindByName(project, channelName));
        }

        public async Task<List<Channel>> GetChannelsForProject(string projectIdOrHref, int take = 30)
        {
            var project = await _octopusHelper.Projects.GetProject(projectIdOrHref);
            var channels = await _octopusHelper.Client.List<ChannelResource>(project.Link("Channels"), new { take = 9999 }, CancellationToken.None);
            return channels.Items.Select(ConvertChannel).ToList();
        }

        public async Task<(bool Success, IEnumerable<Release> Releases)> RemoveChannel(string channelId)
        {
            var channel = await _octopusHelper.Client.Repository.Channels.Get(channelId, CancellationToken.None);
            var allReleases = await _octopusHelper.Client.Repository.Channels.GetAllReleases(channel);
            if (!allReleases.Any())
            {
                await _octopusHelper.Client.Repository.Channels.Delete(channel, CancellationToken.None);
                return (true, null);
            }

            return (false, allReleases.Select(async r => await _octopusHelper.ReleasesInternal.ConvertRelease(r)).Select(t => t.Result));
        }

        private Channel ConvertChannel(ChannelResource channel)
        {
            if (channel == null)
            {
                return null;
            }
            var versionRange = string.Empty;
            var versionTag = string.Empty;
            if (channel.Rules.Any())
            {
                versionRange = channel.Rules[0].VersionRange;
                versionTag = channel.Rules[0].Tag;
            }
            return new Channel { Id = channel.Id, Name = channel.Name, VersionRange = versionRange, VersionTag = versionTag };
        }
    }
}