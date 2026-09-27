using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class EnableEnvironmentRunnerTests
{
    [Test]
    public async Task Run_EnablesEnvironmentMachines_WhenMachineIsNotSpecified()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var machines = Substitute.For<IMachineRepository>();
        helper.Machines.Returns(machines);
        var runner = new EnableEnvironmentRunner(helper, TestLanguageProvider.Create());
        var interaction = Substitute.For<ICommandInteraction>();

        var result = await runner.Run(new Environment { Id = "Environments-1", Name = "Prod" }, null, true, interaction);

        Assert.That(result, Is.EqualTo(0));
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
        await machines.Received(1).EnableMachines("Environments-1");
        await machines.DidNotReceiveWithAnyArgs().EnableMachine(default, default);
    }

    [Test]
    public async Task Run_EnablesMachine_WhenMachineIsSpecified()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var machines = Substitute.For<IMachineRepository>();
        helper.Machines.Returns(machines);
        machines.EnableMachine("Machines-1", "Environments-1").Returns(true);
        var runner = new EnableEnvironmentRunner(helper, TestLanguageProvider.Create());
        var interaction = Substitute.For<ICommandInteraction>();

        var result = await runner.Run(
            new Environment { Id = "Environments-1", Name = "Prod" },
            new Machine { Id = "Machines-1", Name = "Worker" },
            true,
            interaction);

        Assert.That(result, Is.EqualTo(0));
        await machines.Received(1).EnableMachine("Machines-1", "Environments-1");
        await machines.DidNotReceiveWithAnyArgs().EnableMachines(default);
    }

    [Test]
    public async Task Run_ReturnsFailure_WhenMachineCannotBeEnabledInEnvironment()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var machines = Substitute.For<IMachineRepository>();
        helper.Machines.Returns(machines);
        machines.EnableMachine("Machines-1", "Environments-1").Returns(false);
        var runner = new EnableEnvironmentRunner(helper, TestLanguageProvider.Create());
        var interaction = Substitute.For<ICommandInteraction>();

        var result = await runner.Run(
            new Environment { Id = "Environments-1", Name = "Prod" },
            new Machine { Id = "Machines-1", Name = "Worker" },
            true,
            interaction);

        Assert.That(result, Is.EqualTo(-1));
    }

    [Test]
    public async Task Run_DoesNotEnableEnvironment_WhenConfirmationIsRejected()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var machines = Substitute.For<IMachineRepository>();
        var interaction = Substitute.For<ICommandInteraction>();
        helper.Machines.Returns(machines);
        interaction.Confirm(Arg.Any<string>(), false).Returns(false);
        var runner = new EnableEnvironmentRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run(new Environment { Id = "Environments-1", Name = "Prod" }, null, false, interaction);

        Assert.That(result, Is.EqualTo(0));
        interaction.Received(1).Confirm(Arg.Any<string>(), false);
        await machines.DidNotReceive().EnableMachines(Arg.Any<string>());
    }
}
