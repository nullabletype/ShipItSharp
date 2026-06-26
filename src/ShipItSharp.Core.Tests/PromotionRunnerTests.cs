using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Deployment;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.JobRunners.JobConfigs;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class PromotionRunnerTests
{
    [Test]
    public async Task Run_DoesNotUpdateReleaseVariables_WhenOptionIsDisabled()
    {
        var context = CreateContext();
        var config = CreateConfig(updateVariables: false);

        var result = await context.Runner.Run(config, context.ProgressBar, context.Interaction);

        Assert.That(result, Is.EqualTo(0));
        await context.Releases.DidNotReceiveWithAnyArgs().UpdateReleaseVariables(default);
        await context.Deployer.Received(1).StartJob(Arg.Any<EnvironmentDeployment>(), context.UiLogger);
    }

    [Test]
    public async Task Run_UpdatesOnlySelectedReleaseVariables_WhenOptionIsEnabled()
    {
        var context = CreateContext();
        var config = CreateConfig(updateVariables: true);

        var result = await context.Runner.Run(config, context.ProgressBar, context.Interaction);

        Assert.That(result, Is.EqualTo(0));
        await context.Releases.Received(1).UpdateReleaseVariables("Releases-1");
        await context.Releases.DidNotReceive().UpdateReleaseVariables("Releases-2");
        await context.Deployer.Received(1).StartJob(
            Arg.Is<EnvironmentDeployment>(deployment =>
                deployment.ProjectDeployments.Count == 1 &&
                deployment.ProjectDeployments[0].ReleaseId == "Releases-1"),
            context.UiLogger);
    }

    [Test]
    public async Task Run_AddsMachineToDeployment_WhenMachineIsConfigured()
    {
        var context = CreateContext();
        var config = CreateConfig(updateVariables: false, new Machine { Id = "Machines-1", Name = "web-01" });

        var result = await context.Runner.Run(config, context.ProgressBar, context.Interaction);

        Assert.That(result, Is.EqualTo(0));
        await context.Deployer.Received(1).StartJob(
            Arg.Is<EnvironmentDeployment>(deployment =>
                deployment.MachineId == "Machines-1" &&
                deployment.MachineName == "web-01"),
            context.UiLogger);
    }

    [Test]
    public async Task Run_DoesNotStartDeployment_WhenReleaseVariableUpdateFails()
    {
        var context = CreateContext(updateVariablesResult: false);
        var config = CreateConfig(updateVariables: true);

        var result = await context.Runner.Run(config, context.ProgressBar, context.Interaction);

        Assert.That(result, Is.EqualTo(-1));
        await context.Releases.Received(1).UpdateReleaseVariables("Releases-1");
        await context.Deployer.DidNotReceiveWithAnyArgs().StartJob(default, default);
    }

    private static PromotionConfig CreateConfig(bool updateVariables, Machine machine = null)
    {
        return PromotionConfig.Create(
            new Environment { Id = "Environments-2", Name = "Test" },
            new Environment { Id = "Environments-1", Name = "Dev" },
            null,
            runningInteractively: false,
            updateVariables: updateVariables,
            machine: machine).Value;
    }

    private static TestContext CreateContext(bool updateVariablesResult = true)
    {
        var helper = Substitute.For<IOctopusHelper>();
        var projects = Substitute.For<IProjectRepository>();
        var releases = Substitute.For<IReleaseRepository>();
        var deployer = Substitute.For<IDeployer>();
        var uiLogger = Substitute.For<IUiLogger>();
        var progressBar = Substitute.For<IProgressBar>();
        var interaction = Substitute.For<ICommandInteraction>();

        helper.Projects.Returns(projects);
        helper.Releases.Returns(releases);

        var projectStub1 = new ProjectStub { ProjectId = "Projects-1", ProjectName = "Payments", ProjectGroupId = "ProjectGroups-1" };
        var projectStub2 = new ProjectStub { ProjectId = "Projects-2", ProjectName = "Orders", ProjectGroupId = "ProjectGroups-1" };
        projects.GetProjectStubs().Returns(Task.FromResult(new List<ProjectStub> { projectStub1, projectStub2 }));

        projects.ConvertProject(projectStub1, "Environments-1", null, null)
            .Returns(Task.FromResult(CreateProject("Projects-1", "Payments", "Releases-1")));
        projects.ConvertProject(projectStub1, "Environments-2", null, null)
            .Returns(Task.FromResult(CreateProject("Projects-1", "Payments", null)));
        projects.ConvertProject(projectStub2, "Environments-1", null, null)
            .Returns(Task.FromResult(CreateProject("Projects-2", "Orders", "Releases-2")));
        projects.ConvertProject(projectStub2, "Environments-2", null, null)
            .Returns(Task.FromResult(CreateProject("Projects-2", "Orders", "Releases-2")));

        deployer.CheckDeployment(Arg.Any<EnvironmentDeployment>())
            .Returns(Task.FromResult(new DeploymentCheckResult { Success = true }));
        releases.UpdateReleaseVariables("Releases-1").Returns(Task.FromResult(updateVariablesResult));

        return new TestContext(
            new PromotionRunner(TestLanguageProvider.Create(), helper, deployer, uiLogger),
            releases,
            deployer,
            uiLogger,
            progressBar,
            interaction);
    }

    private static Project CreateProject(string projectId, string projectName, string releaseId)
    {
        return new Project
        {
            ProjectId = projectId,
            ProjectName = projectName,
            LifeCycleId = "Lifecycles-1",
            CurrentRelease = releaseId == null
                ? null
                : new Release
                {
                    Id = releaseId,
                    ProjectId = projectId,
                    Version = "1.0.0"
                }
        };
    }

    private sealed record TestContext(
        PromotionRunner Runner,
        IReleaseRepository Releases,
        IDeployer Deployer,
        IUiLogger UiLogger,
        IProgressBar ProgressBar,
        ICommandInteraction Interaction);
}
