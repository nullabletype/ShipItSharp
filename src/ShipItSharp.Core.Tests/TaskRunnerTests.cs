using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Octopus.Interfaces;
using DeploymentModel = ShipItSharp.Core.Deployment.Models.Deployment;
using DeploymentTaskStatus = ShipItSharp.Core.Deployment.Models.TaskStatus;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class TaskRunnerTests
{
    [Test]
    public async Task PrioritiseQueuedTasks_PrioritisesQueuedTasksForEnvironment()
    {
        var (helper, environments, deployments, progressBar) = CreateDependencies();
        environments.GetEnvironment("Prod").Returns(Task.FromResult(new Environment { Id = "Environments-1", Name = "Prod" }));
        deployments.GetDeploymentTasks(0, 100).Returns(Task.FromResult<IEnumerable<TaskStub>>(new List<TaskStub>
        {
            new() { TaskId = "ServerTasks-1", DeploymentId = "Deployments-1", State = DeploymentTaskStatus.Queued },
            new() { TaskId = "ServerTasks-2", DeploymentId = "Deployments-2", State = DeploymentTaskStatus.InProgress },
            new() { TaskId = "ServerTasks-3", DeploymentId = "Deployments-3", State = DeploymentTaskStatus.Queued }
        }));
        deployments.GetDeployments(Arg.Any<string[]>()).Returns(Task.FromResult<IEnumerable<DeploymentModel>>(new List<DeploymentModel>
        {
            new() { TaskId = "ServerTasks-1", EnvironmentId = "Environments-1" },
            new() { TaskId = "ServerTasks-3", EnvironmentId = "Environments-2" }
        }));

        var runner = new TaskRunner(helper);

        var result = await runner.PrioritiseQueuedTasks("Prod", progressBar, Messages());

        Assert.That(result.Found, Is.True);
        Assert.That(result.AffectedTaskIds, Is.EqualTo(new[] { "ServerTasks-1" }));
        await deployments.Received(1).PrioritiseTask("ServerTasks-1");
        await deployments.DidNotReceive().PrioritiseTask("ServerTasks-3");
        await deployments.DidNotReceive().CancelTask(Arg.Any<string>());
        progressBar.Received().CleanCurrentLine();
    }

    [Test]
    public async Task CancelQueuedTasks_CancelsQueuedTasksForEnvironment()
    {
        var (helper, environments, deployments, progressBar) = CreateDependencies();
        environments.GetEnvironment("Prod").Returns(Task.FromResult(new Environment { Id = "Environments-1", Name = "Prod" }));
        deployments.GetDeploymentTasks(0, 100).Returns(Task.FromResult<IEnumerable<TaskStub>>(new List<TaskStub>
        {
            new() { TaskId = "ServerTasks-1", DeploymentId = "Deployments-1", State = DeploymentTaskStatus.Queued }
        }));
        deployments.GetDeployments(Arg.Any<string[]>()).Returns(Task.FromResult<IEnumerable<DeploymentModel>>(new List<DeploymentModel>
        {
            new() { TaskId = "ServerTasks-1", EnvironmentId = "Environments-1" }
        }));

        var runner = new TaskRunner(helper);

        var result = await runner.CancelQueuedTasks("Prod", progressBar, Messages());

        Assert.That(result.Found, Is.True);
        Assert.That(result.AffectedTaskIds, Is.EqualTo(new[] { "ServerTasks-1" }));
        await deployments.Received(1).CancelTask("ServerTasks-1");
        await deployments.DidNotReceive().PrioritiseTask(Arg.Any<string>());
    }

    [Test]
    public async Task PrioritiseQueuedTasks_ReturnsNotFound_WhenEnvironmentDoesNotExist()
    {
        var (helper, environments, deployments, progressBar) = CreateDependencies();
        environments.GetEnvironment("missing").Returns(Task.FromResult<Environment>(null));

        var runner = new TaskRunner(helper);

        var result = await runner.PrioritiseQueuedTasks("missing", progressBar, Messages());

        Assert.That(result.Found, Is.False);
        await deployments.DidNotReceive().GetDeploymentTasks(Arg.Any<int>(), Arg.Any<int>());
        await deployments.DidNotReceive().PrioritiseTask(Arg.Any<string>());
    }

    [Test]
    public async Task PrioritiseQueuedTasks_ReadsNextPage_WhenFirstPageIsFull()
    {
        var (helper, environments, deployments, progressBar) = CreateDependencies();
        environments.GetEnvironment("Prod").Returns(Task.FromResult(new Environment { Id = "Environments-1", Name = "Prod" }));
        var firstPage = new List<TaskStub>();
        for (var index = 0; index < 100; index++)
        {
            firstPage.Add(new TaskStub { TaskId = $"ServerTasks-{index}", DeploymentId = $"Deployments-{index}", State = DeploymentTaskStatus.InProgress });
        }

        deployments.GetDeploymentTasks(0, 100).Returns(Task.FromResult<IEnumerable<TaskStub>>(firstPage));
        deployments.GetDeploymentTasks(100, 100).Returns(Task.FromResult<IEnumerable<TaskStub>>(new List<TaskStub>
        {
            new() { TaskId = "ServerTasks-101", DeploymentId = "Deployments-101", State = DeploymentTaskStatus.Queued }
        }));
        deployments.GetDeployments(Arg.Any<string[]>()).Returns(Task.FromResult<IEnumerable<DeploymentModel>>(new List<DeploymentModel>
        {
            new() { TaskId = "ServerTasks-101", EnvironmentId = "Environments-1" }
        }));

        var runner = new TaskRunner(helper);

        var result = await runner.PrioritiseQueuedTasks("Prod", progressBar, Messages());

        Assert.That(result.AffectedTaskIds, Is.EqualTo(new[] { "ServerTasks-101" }));
        await deployments.Received(1).GetDeploymentTasks(100, 100);
    }

    private static (IOctopusHelper Helper, IEnvironmentRepository Environments, IDeploymentRepository Deployments, IProgressBar ProgressBar) CreateDependencies()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var environments = Substitute.For<IEnvironmentRepository>();
        var deployments = Substitute.For<IDeploymentRepository>();
        var progressBar = Substitute.For<IProgressBar>();

        helper.Environments.Returns(environments);
        helper.Deployments.Returns(deployments);

        return (helper, environments, deployments, progressBar);
    }

    private static TaskRunnerMessages Messages()
    {
        return new TaskRunnerMessages
        {
            LoadingQueuedTasks = "loading tasks",
            LoadingDeployments = "loading deployments",
            ProcessingTask = "processing {0}"
        };
    }
}
