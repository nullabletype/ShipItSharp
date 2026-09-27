using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Octopus.Interfaces;
using DeploymentModel = ShipItSharp.Core.Deployment.Models.Deployment;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class ExistingConfirmationRunnerTests
{
    [Test]
    public async Task DeleteEnvironment_DoesNotReadInput_WhenNoPromptIsSpecified()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var environments = Substitute.For<IEnvironmentRepository>();
        var teams = Substitute.For<ITeamsRepository>();
        var lifecycles = Substitute.For<ILifeCycleRepository>();
        var interaction = Substitute.For<ICommandInteraction>();
        helper.Environments.Returns(environments);
        helper.Teams.Returns(teams);
        helper.LifeCycles.Returns(lifecycles);
        environments.GetEnvironment("Environments-1").Returns(Task.FromResult(new Environment { Id = "Environments-1", Name = "Prod" }));
        var runner = new DeleteEnvironmentRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run("Environments-1", true, interaction);

        Assert.That(result, Is.EqualTo(0));
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
        await environments.Received(1).DeleteEnvironment("Environments-1");
    }

    [Test]
    public async Task RenameRelease_DoesNotReadInput_WhenNoPromptIsSpecified()
    {
        var (helper, projects, releases) = CreateReleaseDependencies();
        var interaction = Substitute.For<ICommandInteraction>();
        releases.RenameRelease("Releases-1", "2.0.0").Returns((string.Empty, true));
        var config = RenameReleaseConfig.Create(string.Empty, new Environment { Id = "Environments-1", Name = "Prod" }, false, "2.0.0").Value;
        var runner = new RenameReleaseRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run(config, Substitute.For<IProgressBar>(), interaction, true);

        Assert.That(result, Is.EqualTo(0));
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
        await releases.Received(1).RenameRelease("Releases-1", "2.0.0");
    }

    [Test]
    public async Task UpdateReleaseVariables_DoesNotReadInput_WhenNoPromptIsSpecified()
    {
        var (helper, projects, releases) = CreateReleaseDependencies();
        var interaction = Substitute.For<ICommandInteraction>();
        releases.UpdateReleaseVariables("Releases-1").Returns(true);
        var runner = new UpdateReleaseVariablesRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run(
            new Environment { Id = "Environments-1", Name = "Prod" },
            string.Empty,
            true,
            Substitute.For<IProgressBar>(),
            interaction);

        Assert.That(result, Is.EqualTo(0));
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
        await releases.Received(1).UpdateReleaseVariables("Releases-1");
    }

    private static (IOctopusHelper Helper, IProjectRepository Projects, IReleaseRepository Releases) CreateReleaseDependencies()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var projects = Substitute.For<IProjectRepository>();
        var releases = Substitute.For<IReleaseRepository>();
        helper.Projects.Returns(projects);
        helper.Releases.Returns(releases);
        projects.GetProjectStubs().Returns(Task.FromResult(new List<ProjectStub>
        {
            new() { ProjectId = "Projects-1", ProjectName = "Project" }
        }));
        releases.GetReleasedVersion("Projects-1", "Environments-1")
            .Returns(Task.FromResult((new Release { Id = "Releases-1", Version = "1.0.0" }, (DeploymentModel)null)));
        return (helper, projects, releases);
    }
}
