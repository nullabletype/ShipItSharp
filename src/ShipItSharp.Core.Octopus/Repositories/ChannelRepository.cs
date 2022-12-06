using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Octopus.Client.Model;
using ShipItSharp.Core.Models;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Repositories;

public class ChannelRepository : IChannelRepository
{
    private OctopusHelper octopusHelper;

    public ChannelRepository(OctopusHelper octopusHelper)
    {
        this.octopusHelper = octopusHelper;
    }

    public async Task<Channel> GetChannelByProjectNameAndChannelName(string name, string channelName) 
    {
        var project = await octopusHelper.client.Repository.Projects.FindOne(resource => resource.Name == name, CancellationToken.None);
        return ConvertChannel(await octopusHelper.client.Repository.Channels.FindByName(project, channelName));
    }

    public async Task<Channel> GetChannelByName(string projectIdOrName, string channelName) 
    {
        var project = await octopusHelper.Projects.GetProject(projectIdOrName);
        return ConvertChannel(await octopusHelper.client.Repository.Channels.FindByName(project, channelName));
    }

    public async Task<List<Channel>> GetChannelsForProject(string projectIdOrHref, int take = 30) 
    {
        var project = await octopusHelper.Projects.GetProject(projectIdOrHref);
        var channels = await octopusHelper.client.List<ChannelResource>(project.Link("Channels"), new { take = 9999 }, CancellationToken.None);
        return channels.Items.Select(ConvertChannel).ToList();
    }

    public async Task<(bool Success, IEnumerable<Release> Releases)> RemoveChannel(string channelId)
    {
        var channel = await octopusHelper.client.Repository.Channels.Get(channelId, CancellationToken.None);
        var allReleases = await octopusHelper.client.Repository.Channels.GetAllReleases(channel);
        if (!allReleases.Any())
        {
            await octopusHelper.client.Repository.Channels.Delete(channel, CancellationToken.None);
            return (true, null);
        }

        return (false, allReleases.Select(async r => await octopusHelper.ConvertRelease(r)).Select(t => t.Result));
    }

    private Channel ConvertChannel(ChannelResource channel)
    {
        if (channel == null)
        {
            return null;
        }
        var versionRange = String.Empty;
        var versionTag = String.Empty;
        if (channel.Rules.Any())
        {
            versionRange = channel.Rules[0].VersionRange;
            versionTag = channel.Rules[0].Tag;
        }
        return new Channel {Id = channel.Id, Name = channel.Name, VersionRange = versionRange, VersionTag = versionTag};
    }
}