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
public class DeploySpecificRunnerTests
{
    [Test]
    public async Task Run_AddsMachineToDeployment_WhenMachineIsConfigured()
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

        var projectStub = new ProjectStub { ProjectId = "Projects-1", ProjectName = "Payments", ProjectGroupId = "ProjectGroups-1" };
        projects.GetProjectStubs().Returns(Task.FromResult(new List<ProjectStub> { projectStub }));
        projects.ConvertProject(projectStub, "Environments-1", null, null)
            .Returns(Task.FromResult(new Project
            {
                ProjectId = "Projects-1",
                ProjectName = "Payments",
                LifeCycleId = "Lifecycles-1",
                CurrentRelease = new Release { Id = "Releases-1", ProjectId = "Projects-1", Version = "1.0.0" }
            }));
        releases.GetRelease("1.0.1", Arg.Any<Project>())
            .Returns(Task.FromResult(new Release { Id = "Releases-2", ProjectId = "Projects-1", Version = "1.0.1" }));
        deployer.CheckDeployment(Arg.Any<EnvironmentDeployment>())
            .Returns(Task.FromResult(new DeploymentCheckResult { Success = true }));

        var config = DeploySpecificConfig.Create(
            new Environment { Id = "Environments-1", Name = "Test" },
            "1.0.1",
            null,
            runningInteractively: false,
            fallbackToDefaultChannel: null,
            machine: new Machine { Id = "Machines-1", Name = "web-01" }).Value;

        var runner = new DeploySpecificRunner(TestLanguageProvider.Create(), helper, deployer, uiLogger);

        var result = await runner.Run(config, progressBar, interaction);

        Assert.That(result, Is.EqualTo(0));
        await deployer.Received(1).StartJob(
            Arg.Is<EnvironmentDeployment>(deployment =>
                deployment.MachineId == "Machines-1" &&
                deployment.MachineName == "web-01"),
            uiLogger);
    }
}
