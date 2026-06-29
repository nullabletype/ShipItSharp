using System.Collections.Generic;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Console.Commands.SubCommands;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Octopus.Interfaces;
using DeploymentEnvironment = ShipItSharp.Core.Deployment.Models.Environment;

namespace ShipItSharp.Console.Tests;

[TestFixture]
public class EnvironmentCommandTests
{
    [Test]
    public async Task Execute_EnvDisable_DisablesEnvironmentResolvedByPartialName()
    {
        var (app, environments, machines) = CreateApp();

        var result = app.Execute("disable", "-e", "Pro");

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).DisableMachines("Environments-1");
        await machines.DidNotReceiveWithAnyArgs().DisableMachine(default, default);
    }

    [Test]
    public async Task Execute_EnvDisable_DisablesEnvironmentResolvedById()
    {
        var (app, environments, machines) = CreateApp();

        var result = app.Execute("disable", "-e", "Environments-1");

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).DisableMachines("Environments-1");
    }

    [Test]
    public async Task Execute_EnvDisable_AcceptsEnvironmentLongOption()
    {
        var (app, environments, machines) = CreateApp();

        var result = app.Execute("disable", "--environment", "Pro");

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).DisableMachines("Environments-1");
    }

    [Test]
    public async Task Execute_EnvDisableMachine_DisablesMachineInResolvedEnvironment()
    {
        var (app, environments, machines) = CreateApp();

        var result = app.Execute("disable", "-e", "Pro", "-m", "Worker");

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).DisableMachine("Machines-1", "Environments-1");
        await machines.DidNotReceive().DisableMachines(Arg.Any<string>());
    }

    [Test]
    public async Task Execute_EnvEnable_EnablesEnvironmentResolvedByPartialName()
    {
        var (app, environments, machines) = CreateApp();

        var result = app.Execute("enable", "-e", "Pro");

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).EnableMachines("Environments-1");
        await machines.DidNotReceiveWithAnyArgs().EnableMachine(default, default);
    }

    [Test]
    public async Task Execute_EnvEnable_EnablesEnvironmentResolvedById()
    {
        var (app, environments, machines) = CreateApp();

        var result = app.Execute("enable", "-e", "Environments-1");

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).EnableMachines("Environments-1");
    }

    [Test]
    public async Task Execute_EnvEnable_AcceptsEnvironmentLongOption()
    {
        var (app, environments, machines) = CreateApp();

        var result = app.Execute("enable", "--environment", "Pro");

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).EnableMachines("Environments-1");
    }

    [Test]
    public async Task Execute_EnvEnableMachine_EnablesMachineInResolvedEnvironment()
    {
        var (app, environments, machines) = CreateApp();

        var result = app.Execute("enable", "-e", "Pro", "-m", "Worker");

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).EnableMachine("Machines-1", "Environments-1");
        await machines.DidNotReceive().EnableMachines(Arg.Any<string>());
    }

    private static (CommandLineApplication App, IEnvironmentRepository Environments, IMachineRepository Machines) CreateApp()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var environments = Substitute.For<IEnvironmentRepository>();
        var machines = Substitute.For<IMachineRepository>();

        helper.Environments.Returns(environments);
        helper.Machines.Returns(machines);

        var environment = new DeploymentEnvironment { Id = "Environments-1", Name = "Prod" };
        environments.GetEnvironment("Environments-1").Returns(Task.FromResult(environment));
        environments.GetEnvironment("Pro").Returns(Task.FromResult<DeploymentEnvironment>(null));
        environments.GetMatchingEnvironments("Pro").Returns(Task.FromResult(new List<DeploymentEnvironment> { environment }));
        machines.GetMachine("Worker", "Environments-1").Returns(Task.FromResult(new Machine { Id = "Machines-1", Name = "Worker" }));
        machines.DisableMachine("Machines-1", "Environments-1").Returns(true);
        machines.EnableMachine("Machines-1", "Environments-1").Returns(true);

        var languageProvider = TestLanguageProvider.Create();
        var disableRunner = new DisableEnvironmentRunner(helper, languageProvider);
        var enableRunner = new EnableEnvironmentRunner(helper, languageProvider);
        var disable = new DisableEnvironment(helper, languageProvider, disableRunner);
        var enable = new EnableEnvironment(helper, languageProvider, enableRunner);

        var app = new CommandLineApplication();
        app.Command(disable.CommandName, disable.Configure);
        app.Command(enable.CommandName, enable.Configure);

        return (app, environments, machines);
    }
}
