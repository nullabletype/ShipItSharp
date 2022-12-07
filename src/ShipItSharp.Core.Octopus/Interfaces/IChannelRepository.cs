using System.Collections.Generic;
using System.Threading.Tasks;
using ShipItSharp.Core.Deployment.Models;

namespace ShipItSharp.Core.Octopus.Interfaces
{
    public interface IChannelRepository
    {
        Task<Channel> GetChannelByProjectNameAndChannelName(string name, string channelName);
        Task<Channel> GetChannelByName(string projectIdOrName, string channelName);
        Task<List<Channel>> GetChannelsForProject(string projectIdOrHref, int take = 30);
        Task<(bool Success, IEnumerable<Release> Releases)> RemoveChannel(string channelId);
    }
}