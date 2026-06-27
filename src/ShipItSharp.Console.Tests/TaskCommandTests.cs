using System.Collections.Generic;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Console.Commands;
using ShipItSharp.Console.Commands.SubCommands;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.Interfaces;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Octopus.Interfaces;
using DeploymentModel = ShipItSharp.Core.Deployment.Models.Deployment;
using DeploymentEnvironment = ShipItSharp.Core.Deployment.Models.Environment;
using DeploymentTaskStatus = ShipItSharp.Core.Deployment.Models.TaskStatus;

namespace ShipItSharp.Console.Tests;

[TestFixture]
public class TaskCommandTests
{
    [Test]
    public async Task Execute_TaskPrioritise_InvokesPrioritiseForQueuedEnvironmentTasks()
    {
        var (app, deployments) = CreateApp();

        var result = app.Execute("task", "prioritise", "-e", "Pro");

        Assert.That(result, Is.EqualTo(0));
        await deployments.Received(1).PrioritiseTask("ServerTasks-1");
        await deployments.DidNotReceive().CancelTask(Arg.Any<string>());
    }

    [Test]
    public async Task Execute_TaskCancel_InvokesCancelForQueuedEnvironmentTasks()
    {
        var (app, deployments) = CreateApp();

        var result = app.Execute("task", "cancel", "-e", "Pro");

        Assert.That(result, Is.EqualTo(0));
        await deployments.Received(1).CancelTask("ServerTasks-1");
        await deployments.DidNotReceive().PrioritiseTask(Arg.Any<string>());
    }

    private static (CommandLineApplication App, IDeploymentRepository Deployments) CreateApp()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var environments = Substitute.For<IEnvironmentRepository>();
        var deployments = Substitute.For<IDeploymentRepository>();
        var progressBar = Substitute.For<IProgressBar>();

        helper.Environments.Returns(environments);
        helper.Deployments.Returns(deployments);

        environments.GetMatchingEnvironments("Pro").Returns(Task.FromResult(new List<DeploymentEnvironment>
        {
            new() { Id = "Environments-1", Name = "Prod" }
        }));
        environments.GetEnvironment("Environments-1").Returns(Task.FromResult(new DeploymentEnvironment { Id = "Environments-1", Name = "Prod" }));
        deployments.GetDeploymentTasks(0, 100).Returns(Task.FromResult<IEnumerable<TaskStub>>(new List<TaskStub>
        {
            new() { TaskId = "ServerTasks-1", DeploymentId = "Deployments-1", State = DeploymentTaskStatus.Queued }
        }));
        deployments.GetDeployments(Arg.Any<string[]>()).Returns(Task.FromResult<IEnumerable<DeploymentModel>>(new List<DeploymentModel>
        {
            new() { TaskId = "ServerTasks-1", EnvironmentId = "Environments-1" }
        }));

        var languageProvider = TestLanguageProvider.Create();
        var runner = new TaskRunner(helper);
        var prioritise = new PrioritiseTask(helper, languageProvider, progressBar, runner);
        var cancel = new CancelTask(helper, languageProvider, progressBar, runner);
        var task = new TaskCommand(helper, prioritise, cancel, languageProvider);

        var app = new CommandLineApplication();
        app.Command(task.CommandName, task.Configure);

        return (app, deployments);
    }
}
