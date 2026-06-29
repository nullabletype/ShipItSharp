using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class DisableEnvironmentRunnerTests
{
    [Test]
    public async Task Run_DisablesEnvironmentMachines_WhenMachineIsNotSpecified()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var environments = Substitute.For<IEnvironmentRepository>();
        var machines = Substitute.For<IMachineRepository>();
        helper.Environments.Returns(environments);
        helper.Machines.Returns(machines);
        var runner = new DisableEnvironmentRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run(new Environment { Id = "Environments-1", Name = "Prod" });

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).DisableMachines("Environments-1");
        await environments.DidNotReceiveWithAnyArgs().DeleteEnvironment(default);
        await machines.DidNotReceiveWithAnyArgs().DisableMachine(default, default);
    }

    [Test]
    public async Task Run_DisablesMachine_WhenMachineIsSpecified()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var environments = Substitute.For<IEnvironmentRepository>();
        var machines = Substitute.For<IMachineRepository>();
        helper.Environments.Returns(environments);
        helper.Machines.Returns(machines);
        machines.DisableMachine("Machines-1", "Environments-1").Returns(true);
        var runner = new DisableEnvironmentRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run(
            new Environment { Id = "Environments-1", Name = "Prod" },
            new Machine { Id = "Machines-1", Name = "Worker" });

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).DisableMachine("Machines-1", "Environments-1");
        await machines.DidNotReceiveWithAnyArgs().DisableMachines(default);
    }

    [Test]
    public async Task Run_ReturnsFailure_WhenMachineCannotBeDisabledInEnvironment()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var machines = Substitute.For<IMachineRepository>();
        helper.Machines.Returns(machines);
        machines.DisableMachine("Machines-1", "Environments-1").Returns(false);
        var runner = new DisableEnvironmentRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run(
            new Environment { Id = "Environments-1", Name = "Prod" },
            new Machine { Id = "Machines-1", Name = "Worker" });

        Assert.That(result, Is.EqualTo(-1));
    }
}
