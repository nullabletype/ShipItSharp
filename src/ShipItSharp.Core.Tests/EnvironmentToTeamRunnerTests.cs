using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class EnvironmentToTeamRunnerTests
{
    [Test]
    public async Task Run_DoesNotPrompt_WhenTeamIsMissing()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var teams = Substitute.For<ITeamsRepository>();
        var interaction = Substitute.For<ICommandInteraction>();
        helper.Teams.Returns(teams);
        var runner = new EnvironmentToTeamRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run("Environments-1", string.Empty, false, interaction);

        Assert.That(result, Is.EqualTo(-1));
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
        await teams.DidNotReceive().AddEnvironmentToTeam(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Run_DoesNotAddEnvironment_WhenConfirmationIsRejected()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var teams = Substitute.For<ITeamsRepository>();
        var interaction = Substitute.For<ICommandInteraction>();
        helper.Teams.Returns(teams);
        interaction.Confirm(Arg.Any<string>(), false).Returns(false);
        var runner = new EnvironmentToTeamRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run("Environments-1", "Teams-1", false, interaction);

        Assert.That(result, Is.EqualTo(0));
        interaction.Received(1).Confirm(Arg.Any<string>(), false);
        await teams.DidNotReceive().AddEnvironmentToTeam(Arg.Any<string>(), Arg.Any<string>());
    }

    [Test]
    public async Task Run_AddsEnvironmentWithoutReadingInput_WhenNoPromptIsSpecified()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var teams = Substitute.For<ITeamsRepository>();
        var interaction = Substitute.For<ICommandInteraction>();
        helper.Teams.Returns(teams);
        var runner = new EnvironmentToTeamRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run("Environments-1", "Teams-1", true, interaction);

        Assert.That(result, Is.EqualTo(0));
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
        await teams.Received(1).AddEnvironmentToTeam("Environments-1", "Teams-1");
    }
}
