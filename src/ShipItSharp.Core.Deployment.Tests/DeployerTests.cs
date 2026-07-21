using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.Deployment.Interfaces;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Language;
using ShipItSharp.Core.Octopus.Interfaces;
using EnvironmentModel = ShipItSharp.Core.Deployment.Models.Environment;
using LifeCycleModel = ShipItSharp.Core.Deployment.Models.LifeCycle;
using TaskStatus = ShipItSharp.Core.Deployment.Models.TaskStatus;

namespace ShipItSharp.Core.Deployment.Tests;

[TestFixture]
public class DeployerTests
{
    [Test]
    public async Task CheckDeployment_AllowsDeploymentToFirstPhaseEnvironment()
    {
        var helper = CreateHelperWithLifecycle(new LifeCycleModel
        {
            Phases =
            {
                new Phase { OptionalDeploymentTargetEnvironmentIds = new List<string> { "Environments-1" } }
            }
        });
        var deployer = CreateDeployer(helper, CreateDeploymentOutputLanguageProvider());

        var result = await deployer.CheckDeployment(CreateDeployment("Environments-1"));

        Assert.That(result.Success, Is.True);
        await helper.Deployments.DidNotReceiveWithAnyArgs().GetDeployments(default(string));
    }

    [Test]
    public async Task CheckDeployment_AllowsDeploymentToLaterPhase_WhenRequiredPreviousDeploymentExists()
    {
        var helper = CreateHelperWithLifecycle(new LifeCycleModel
        {
            Phases =
            {
                new Phase { OptionalDeploymentTargetEnvironmentIds = new List<string> { "Environments-1" } },
                new Phase { OptionalDeploymentTargetEnvironmentIds = new List<string> { "Environments-2" } }
            }
        });
        helper.Deployments.GetDeployments("Releases-1")
            .Returns(new[] { new Deployment.Models.Deployment { EnvironmentId = "Environments-1" } });
        var deployer = CreateDeployer(helper, CreateDeploymentOutputLanguageProvider());

        var result = await deployer.CheckDeployment(CreateDeployment("Environments-2"));

        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task CheckDeployment_BlocksDeploymentToLaterPhase_WhenRequiredPreviousDeploymentIsMissing()
    {
        var helper = CreateHelperWithLifecycle(new LifeCycleModel
        {
            Phases =
            {
                new Phase { OptionalDeploymentTargetEnvironmentIds = new List<string> { "Environments-1" } },
                new Phase { OptionalDeploymentTargetEnvironmentIds = new List<string> { "Environments-2" } }
            }
        });
        helper.Deployments.GetDeployments("Releases-1")
            .Returns(System.Array.Empty<Deployment.Models.Deployment>());
        var deployer = CreateDeployer(helper, CreateDeploymentOutputLanguageProvider());

        var result = await deployer.CheckDeployment(CreateDeployment("Environments-2"));

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Payments"));
        Assert.That(result.ErrorMessage, Does.Contain("Prod"));
    }

    [Test]
    public void FillRequiredVariables_PromptsUntilValueIsProvided_WhenInteractive()
    {
        var deployer = CreateDeployer(Substitute.For<IOctopusHelper>());
        var project = new ProjectDeployment
        {
            ProjectName = "Payments",
            RequiredVariables = new List<RequiredVariableDeployment>
            {
                new() { Id = "Variables-1", Name = "Password" }
            }
        };
        var prompts = 0;

        deployer.FillRequiredVariables(new List<ProjectDeployment> { project }, _ => ++prompts == 1 ? "" : "secret", true);

        Assert.That(project.RequiredVariables[0].Value, Is.EqualTo("secret"));
        Assert.That(prompts, Is.EqualTo(2));
    }

    [Test]
    public async Task StartJob_CreatesReleaseAndDeploymentTask_WhenReleaseIdIsMissing()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var releases = Substitute.For<IReleaseRepository>();
        var deployments = Substitute.For<IDeploymentRepository>();
        helper.Releases.Returns(releases);
        helper.Deployments.Returns(deployments);
        var project = new ProjectDeployment
        {
            ProjectId = "Projects-1",
            ProjectName = "Payments",
            Packages = new List<PackageDeployment> { new() { PackageName = "1.2.3", StepName = "Deploy" } }
        };
        releases.CreateRelease(project, false)
            .Returns(new Release { Id = "Releases-1", Version = "1.2.i" });
        deployments.CreateDeploymentTask(project, "Environments-1", "Releases-1", false, "Machines-1")
            .Returns(new Deployment.Models.Deployment { TaskId = "ServerTasks-1" });
        deployments.GetTaskDetails("ServerTasks-1")
            .Returns(new TaskDetails { TaskId = "ServerTasks-1", State = TaskStatus.Done });
        deployments.GetTaskRawLog("ServerTasks-1").Returns("raw log");
        var deployer = CreateDeployer(helper, CreateDeploymentOutputLanguageProvider());
        var job = new EnvironmentDeployment
        {
            EnvironmentId = "Environments-1",
            EnvironmentName = "Prod",
            MachineId = "Machines-1",
            MachineName = "web-01",
            DeployAsync = false,
            ProjectDeployments = new List<ProjectDeployment> { project }
        };

        var uiLogger = Substitute.For<IUiLogger>();

        await deployer.StartJob(job, uiLogger);

        await releases.Received(1).CreateRelease(project, false);
        await deployments.Received(1).CreateDeploymentTask(project, "Environments-1", "Releases-1", false, "Machines-1");
        await deployments.Received(1).GetTaskRawLog("ServerTasks-1");
        uiLogger.Received().WriteLine(Arg.Is<string>(line => line.Contains("ShipItSharp deployment to Prod on machine web-01")));
        uiLogger.Received().WriteLine(Arg.Is<string>(line => line.Contains("Creating Octopus task") && line.Contains("Prod on machine web-01")));
        uiLogger.Received().WriteLine(Arg.Is<string>(line => line.Contains("Payments deployed to Prod on machine web-01")));
        uiLogger.Received().WriteLine(Arg.Is<string>(line => line.Contains("Deployment summary")));
        uiLogger.Received().WriteLine(Arg.Is<string>(line => line.Contains("done") && line.Contains("Completed: 1")));
        uiLogger.Received().WriteLine(Arg.Is<string>(line => line.Contains("done") && line.Contains("Failed: 0")));
        uiLogger.Received().WriteLine(Arg.Is<string>(line => line.Contains("run") && line.Contains("Total: 1")));
        uiLogger.Received().WriteLine(Arg.Is<string>(line => line.Contains("run") && line.Contains("Time taken:")));
    }

    [Test]
    public async Task StartJob_ReportsLatestDeploymentQueuePosition_WhenItIsQueued()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var releases = Substitute.For<IReleaseRepository>();
        var deployments = Substitute.For<IDeploymentRepository>();
        helper.Releases.Returns(releases);
        helper.Deployments.Returns(deployments);
        var projects = new[]
        {
            new ProjectDeployment { ProjectId = "Projects-1", ProjectName = "Payments", ReleaseId = "Releases-1" },
            new ProjectDeployment { ProjectId = "Projects-2", ProjectName = "Orders", ReleaseId = "Releases-2" }
        };
        releases.GetRelease("Releases-1").Returns(new Release { Id = "Releases-1", Version = "1.0.0" });
        releases.GetRelease("Releases-2").Returns(new Release { Id = "Releases-2", Version = "1.0.0" });
        deployments.CreateDeploymentTask(projects[0], "Environments-1", "Releases-1", false, null)
            .Returns(new Deployment.Models.Deployment { TaskId = "ServerTasks-2" });
        deployments.CreateDeploymentTask(projects[1], "Environments-1", "Releases-2", false, null)
            .Returns(new Deployment.Models.Deployment { TaskId = "ServerTasks-3" });
        deployments.GetTaskDetails("ServerTasks-2").Returns(new TaskDetails { TaskId = "ServerTasks-2", State = TaskStatus.Queued });
        deployments.GetTaskDetails("ServerTasks-3").Returns(new TaskDetails { TaskId = "ServerTasks-3", State = TaskStatus.Queued });
        var queueTime = System.DateTimeOffset.UtcNow;
        var firstPage = Enumerable.Range(1, 99)
            .Select(index => new TaskStub
            {
                TaskId = $"OtherTasks-{index}",
                State = TaskStatus.Queued,
                QueueTime = queueTime.AddMinutes(index)
            })
            .Append(new TaskStub { TaskId = "ServerTasks-2", State = TaskStatus.Queued, QueueTime = queueTime.AddMinutes(100) })
            .ToArray();
        deployments.GetDeploymentTasks(0, 100).Returns(
            firstPage,
            new[]
            {
                new TaskStub { TaskId = "ServerTasks-2", State = TaskStatus.Done },
                new TaskStub { TaskId = "ServerTasks-3", State = TaskStatus.Done }
            });
        deployments.GetDeploymentTasks(100, 100).Returns(
            new[] { new TaskStub { TaskId = "ServerTasks-3", State = TaskStatus.Queued, QueueTime = queueTime.AddMinutes(101) } });
        var deployer = CreateDeployer(helper, CreateDeploymentOutputLanguageProvider());
        var uiLogger = Substitute.For<IUiLogger>();
        var job = new EnvironmentDeployment
        {
            EnvironmentId = "Environments-1",
            EnvironmentName = "Prod",
            DeployAsync = true,
            ProjectDeployments = projects.ToList()
        };

        await deployer.StartJob(job, uiLogger);

        uiLogger.Received(1).WriteLine(Arg.Is<string>(line => line.Contains("Latest deployment is at queue position 101.")));
    }

    [Test]
    public async Task StartJob_PrioritisesAllQueuedTasksInCurrentJob_WhenQueueJumpIsConfirmed()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var releases = Substitute.For<IReleaseRepository>();
        var deployments = Substitute.For<IDeploymentRepository>();
        var interaction = Substitute.For<IDeploymentQueueInteraction>();
        helper.Releases.Returns(releases);
        helper.Deployments.Returns(deployments);
        var projects = new[]
        {
            new ProjectDeployment { ProjectId = "Projects-1", ProjectName = "Payments", ReleaseId = "Releases-1" },
            new ProjectDeployment { ProjectId = "Projects-2", ProjectName = "Orders", ReleaseId = "Releases-2" }
        };
        releases.GetRelease("Releases-1").Returns(new Release { Id = "Releases-1", Version = "1.0.0" });
        releases.GetRelease("Releases-2").Returns(new Release { Id = "Releases-2", Version = "1.0.0" });
        deployments.CreateDeploymentTask(projects[0], "Environments-1", "Releases-1", false, null)
            .Returns(new Deployment.Models.Deployment { TaskId = "ServerTasks-2" });
        deployments.CreateDeploymentTask(projects[1], "Environments-1", "Releases-2", false, null)
            .Returns(new Deployment.Models.Deployment { TaskId = "ServerTasks-3" });
        deployments.GetTaskDetails("ServerTasks-2").Returns(new TaskDetails { TaskId = "ServerTasks-2", State = TaskStatus.Queued });
        deployments.GetTaskDetails("ServerTasks-3").Returns(new TaskDetails { TaskId = "ServerTasks-3", State = TaskStatus.Queued });
        deployments.GetDeploymentTasks(0, 100).Returns(
            new[]
            {
                new TaskStub { TaskId = "ServerTasks-1", State = TaskStatus.Queued, QueueTime = System.DateTimeOffset.UtcNow },
                new TaskStub { TaskId = "ServerTasks-2", State = TaskStatus.Queued, QueueTime = System.DateTimeOffset.UtcNow.AddMinutes(1) },
                new TaskStub { TaskId = "ServerTasks-3", State = TaskStatus.Queued, QueueTime = System.DateTimeOffset.UtcNow.AddMinutes(2) }
            },
            new[]
            {
                new TaskStub { TaskId = "ServerTasks-2", State = TaskStatus.Queued, QueueTime = System.DateTimeOffset.UtcNow },
                new TaskStub { TaskId = "ServerTasks-3", State = TaskStatus.Queued, QueueTime = System.DateTimeOffset.UtcNow.AddMinutes(1) }
            },
            new[]
            {
                new TaskStub { TaskId = "ServerTasks-2", State = TaskStatus.Done },
                new TaskStub { TaskId = "ServerTasks-3", State = TaskStatus.Done }
            });
        interaction.QueueJumpRequested().Returns(true);
        interaction.ConfirmQueueJump(Arg.Any<string>()).Returns(true);
        var deployer = CreateDeployer(helper, CreateDeploymentOutputLanguageProvider(), interaction);
        var uiLogger = Substitute.For<IUiLogger>();
        var job = new EnvironmentDeployment
        {
            EnvironmentId = "Environments-1",
            EnvironmentName = "Prod",
            DeployAsync = true,
            ProjectDeployments = projects.ToList()
        };

        await deployer.StartJob(job, uiLogger);

        interaction.Received(1).Reset();
        interaction.Received(2).QueueJumpRequested();
        interaction.Received(1).ConfirmQueueJump(Arg.Is<string>(prompt => prompt.Contains("all 2 queued task(s)")));
        await deployments.Received(1).PrioritiseTask("ServerTasks-2");
        await deployments.Received(1).PrioritiseTask("ServerTasks-3");
        Received.InOrder(() =>
        {
            deployments.PrioritiseTask("ServerTasks-3");
            deployments.PrioritiseTask("ServerTasks-2");
        });
        uiLogger.Received(1).WriteLine(Arg.Is<string>(line => line.Contains("Moved 2 deployment task(s)")));
    }

    private static IOctopusHelper CreateHelperWithLifecycle(LifeCycleModel lifecycle)
    {
        var helper = Substitute.For<IOctopusHelper>();
        var lifecycles = Substitute.For<ILifeCycleRepository>();
        var deployments = Substitute.For<IDeploymentRepository>();
        helper.LifeCycles.Returns(lifecycles);
        helper.Deployments.Returns(deployments);
        lifecycles.GetLifeCycle("Lifecycles-1").Returns(lifecycle);
        return helper;
    }

    private static EnvironmentDeployment CreateDeployment(string environmentId)
    {
        return new EnvironmentDeployment
        {
            EnvironmentId = environmentId,
            EnvironmentName = "Prod",
            ProjectDeployments = new List<ProjectDeployment>
            {
                new()
                {
                    ProjectName = "Payments",
                    LifeCycleId = "Lifecycles-1",
                    ReleaseId = "Releases-1"
                }
            }
        };
    }

    private static Deployer CreateDeployer(IOctopusHelper helper, ILanguageProvider languageProvider = null, IDeploymentQueueInteraction queueInteraction = null)
    {
        var configuration = Substitute.For<IConfiguration>();
        configuration.OctopusUrl.Returns("https://octopus.example");
        return new Deployer(helper, configuration, languageProvider ?? TestLanguageProvider.Create(), queueInteraction ?? Substitute.For<IDeploymentQueueInteraction>());
    }

    private static ILanguageProvider CreateDeploymentOutputLanguageProvider()
    {
        var values = new Dictionary<string, string>
        {
            ["ShipItSharpDeploymentToTarget"] = "ShipItSharp deployment to {0}",
            ["StatusRun"] = "run",
            ["StatusDone"] = "done",
            ["CreatingReleaseForProject"] = "Creating release for {0}",
            ["CreatedReleaseForProject"] = "Created release {0} {1}",
            ["CreatingOctopusTaskForProject"] = "Creating Octopus task for {0} to {1}",
            ["CreatedOctopusTask"] = "Created Octopus task {0}",
            ["ProjectDeployedToEnvironment"] = "{0} deployed to {1}",
            ["RawLog"] = "Raw log:",
            ["DeploymentComplete"] = "Deployment complete. {0} shipped, {1} failed.",
            ["DeploymentSummary"] = "Deployment summary",
            ["DeploymentSummaryCompleted"] = "Completed: {0}",
            ["DeploymentSummaryFailed"] = "Failed: {0}",
            ["DeploymentSummaryTotal"] = "Total: {0}",
            ["DeploymentElapsedTime"] = "Time taken: {0}",
            ["LatestDeploymentQueuePosition"] = "Latest deployment is at queue position {0}.",
            ["ConfirmQueueJump"] = "Move all {0} queued task(s) from this deployment job to the top of the queue?",
            ["QueueJumpRequested"] = "Moved {0} deployment task(s) to the top of the queue."
        };

        var language = Substitute.For<ILanguageProvider>();
        language.GetString(Arg.Any<LanguageSection>(), Arg.Any<string>())
            .Returns(call => values.GetValueOrDefault(call.ArgAt<string>(1), call.ArgAt<string>(1)));
        return language;
    }
}
