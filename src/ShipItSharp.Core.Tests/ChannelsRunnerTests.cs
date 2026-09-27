using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class ChannelsRunnerTests
{
    [Test]
    public async Task Cleanup_DoesNotRemoveChannels_WhenConfirmationIsRejected()
    {
        var (runner, channels, interaction) = CreateRunner();
        interaction.Confirm(Arg.Any<string>(), false).Returns(false);

        var result = await runner.Cleanup(ChannelCleanupConfig.Create("Group", false).Value, false, interaction);

        Assert.That(result, Is.True);
        interaction.Received(1).Confirm(Arg.Any<string>(), false);
        await channels.DidNotReceive().RemoveChannel(Arg.Any<string>());
    }

    [Test]
    public async Task Cleanup_RemovesChannelsWithoutReadingInput_WhenNoPromptIsSpecified()
    {
        var (runner, channels, interaction) = CreateRunner();
        channels.RemoveChannel("Channels-1").Returns((true, (IEnumerable<Release>)new List<Release>()));

        var result = await runner.Cleanup(ChannelCleanupConfig.Create("Group", false).Value, true, interaction);

        Assert.That(result, Is.True);
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
        await channels.Received(1).RemoveChannel("Channels-1");
    }

    private static (ChannelsRunner Runner, IChannelRepository Channels, ICommandInteraction Interaction) CreateRunner()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var projects = Substitute.For<IProjectRepository>();
        var channels = Substitute.For<IChannelRepository>();
        var packages = Substitute.For<IPackageRepository>();
        var interaction = Substitute.For<ICommandInteraction>();
        helper.Projects.Returns(projects);
        helper.Channels.Returns(channels);
        helper.Packages.Returns(packages);
        projects.GetFilteredProjectGroups("Group").Returns(Task.FromResult(new List<ProjectGroup> { new() { Id = "ProjectGroups-1" } }));
        projects.GetProjectStubs().Returns(Task.FromResult(new List<ProjectStub>
        {
            new() { ProjectId = "Projects-1", ProjectName = "Project", ProjectGroupId = "ProjectGroups-1" }
        }));
        channels.GetChannelsForProject("Projects-1", 9999).Returns(Task.FromResult(new List<Channel>
        {
            new() { Id = "Channels-1", Name = "Obsolete" }
        }));
        packages.GetPackages("Projects-1", null, null, true, 100).Returns(Task.FromResult<IList<PackageStep>>(new List<PackageStep>()));

        var runner = new ChannelsRunner(
            Substitute.For<IProgressBar>(),
            helper,
            TestLanguageProvider.Create(),
            Substitute.For<IUiLogger>());
        return (runner, channels, interaction);
    }
}
