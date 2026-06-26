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
public class DeployRunnerTests
{
    [Test]
    public async Task Run_AddsMachineToDeployment_WhenMachineIsConfigured()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var deployer = Substitute.For<IDeployer>();
        var uiLogger = Substitute.For<IUiLogger>();
        var progressBar = Substitute.For<IProgressBar>();
        var interaction = Substitute.For<ICommandInteraction>();

        var projectRepository = Substitute.For<IProjectRepository>();
        var channelRepository = Substitute.For<IChannelRepository>();
        helper.Projects.Returns(projectRepository);
        helper.Channels.Returns(channelRepository);

        var projectStub = new ProjectStub { ProjectId = "Projects-1", ProjectName = "Payments" };
        projectRepository.ConvertProject(Arg.Any<ProjectStub>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(CreateProjectWithNewPackage()));
        channelRepository.GetChannelByName("Projects-1", "Default")
            .Returns(Task.FromResult(new Channel { Id = "Channels-1", Name = "Default" }));
        deployer.CheckDeployment(Arg.Any<EnvironmentDeployment>())
            .Returns(Task.FromResult(new DeploymentCheckResult { Success = true }));

        var config = DeployConfig.Create(
            new Environment { Id = "Environments-1", Name = "Test" },
            "Default",
            null,
            null,
            null,
            runningInteractively: false,
            machine: new Machine { Id = "Machines-1", Name = "web-01" }).Value;

        var runner = new DeployRunner(TestLanguageProvider.Create(), helper, deployer, uiLogger);

        var result = await runner.Run(config, progressBar, new List<ProjectStub> { projectStub }, interaction);

        Assert.That(result, Is.EqualTo(0));
        await deployer.Received(1).StartJob(
            Arg.Is<EnvironmentDeployment>(deployment =>
                deployment.MachineId == "Machines-1" &&
                deployment.MachineName == "web-01"),
            uiLogger);
    }

    [Test]
    public async Task Run_DoesNotPrecheckProjectWhenNoPackageIsAvailable()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var deployer = Substitute.For<IDeployer>();
        var uiLogger = Substitute.For<IUiLogger>();
        var progressBar = Substitute.For<IProgressBar>();
        var interaction = Substitute.For<ICommandInteraction>();

        var projectRepository = Substitute.For<IProjectRepository>();
        var channelRepository = Substitute.For<IChannelRepository>();
        helper.Projects.Returns(projectRepository);
        helper.Channels.Returns(channelRepository);

        var projectStub = new ProjectStub { ProjectId = "Projects-1", ProjectName = "Payments" };
        var project = new Project
        {
            ProjectId = "Projects-1",
            ProjectName = "Payments",
            CurrentRelease = new Release
            {
                Version = "1.0.0",
                SelectedPackages = new List<PackageStub>
                {
                    new() { StepId = "Step-1", Version = "1.0.0" }
                }
            },
            AvailablePackages = new List<PackageStep>
            {
                new() { StepId = "Step-1", StepName = "Deploy", AvailablePackages = new List<PackageStub>() }
            }
        };

        projectRepository.ConvertProject(Arg.Any<ProjectStub>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(project));
        channelRepository.GetChannelByName(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult<ShipItSharp.Core.Deployment.Models.Channel>(null));

        interaction.SelectDeployProjects(Arg.Any<DeployConfig>(), Arg.Any<IList<Project>>())
            .Returns(call =>
            {
                var projects = call.ArgAt<IList<Project>>(1);
                Assert.That(projects[0].Checked, Is.False, "Project should not be auto-selected when no target package is available.");
                return new List<int>();
            });

        var config = DeployConfig.Create(
            new ShipItSharp.Core.Deployment.Models.Environment { Id = "Environments-1", Name = "Test" },
            "Default",
            null,
            null,
            null,
            runningInteractively: true).Value;

        var runner = new DeployRunner(TestLanguageProvider.Create(), helper, deployer, uiLogger);

        var result = await runner.Run(config, progressBar, new List<ProjectStub> { projectStub }, interaction);

        Assert.That(result, Is.EqualTo(-1));
        _ = deployer.DidNotReceive().CheckDeployment(Arg.Any<EnvironmentDeployment>());
    }

    private static Project CreateProjectWithNewPackage()
    {
        return new Project
        {
            ProjectId = "Projects-1",
            ProjectName = "Payments",
            LifeCycleId = "Lifecycles-1",
            CurrentRelease = new Release
            {
                Version = "1.0.0",
                SelectedPackages = new List<PackageStub>
                {
                    new() { StepId = "Step-1", Version = "1.0.0" }
                }
            },
            AvailablePackages = new List<PackageStep>
            {
                new()
                {
                    StepId = "Step-1",
                    StepName = "Deploy",
                    SelectedPackage = new PackageStub { Id = "Packages-1", StepId = "Step-1", Version = "1.0.1" }
                }
            }
        };
    }
}
