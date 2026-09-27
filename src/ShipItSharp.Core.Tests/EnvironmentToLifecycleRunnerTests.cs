using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.JobRunners;
using ShipItSharp.Core.JobRunners.Interfaces;
using ShipItSharp.Core.Octopus.Interfaces;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class EnvironmentToLifecycleRunnerTests
{
    [Test]
    public async Task Run_ReturnsFailure_WhenPhaseIsNotNumeric()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var lifeCycles = Substitute.For<ILifeCycleRepository>();
        helper.LifeCycles.Returns(lifeCycles);
        var runner = new EnvironmentToLifecycleRunner(helper, TestLanguageProvider.Create());
        var interaction = Substitute.For<ICommandInteraction>();

        var result = await runner.Run("Environments-1", "Lifecycles-1", "abc", false, false, interaction);

        Assert.That(result, Is.EqualTo(-1));
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
        _ = lifeCycles.DidNotReceiveWithAnyArgs().AddEnvironmentToLifecyclePhase(default, default, default, default);
    }

    [Test]
    public async Task Run_MapsLifecycleRepositoryErrors()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var lifeCycles = Substitute.For<ILifeCycleRepository>();
        helper.LifeCycles.Returns(lifeCycles);
        lifeCycles.AddEnvironmentToLifecyclePhase("Environments-1", "Lifecycles-1", 1, true)
            .Returns((false, LifecycleErrorType.PhaseInLifeCycleNotFound, "missing phase"));

        var runner = new EnvironmentToLifecycleRunner(helper, TestLanguageProvider.Create());
        var interaction = Substitute.For<ICommandInteraction>();

        var result = await runner.Run("Environments-1", "Lifecycles-1", "2", true, true, interaction);

        Assert.That(result, Is.EqualTo(-1));
    }

    [Test]
    public async Task Run_ReturnsSuccess_WhenRepositorySucceeds()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var lifeCycles = Substitute.For<ILifeCycleRepository>();
        helper.LifeCycles.Returns(lifeCycles);
        lifeCycles.AddEnvironmentToLifecyclePhase("Environments-1", "Lifecycles-1", 0, false)
            .Returns((true, LifecycleErrorType.None, string.Empty));

        var runner = new EnvironmentToLifecycleRunner(helper, TestLanguageProvider.Create());
        var interaction = Substitute.For<ICommandInteraction>();

        var result = await runner.Run("Environments-1", "Lifecycles-1", "1", false, true, interaction);

        Assert.That(result, Is.EqualTo(0));
        interaction.DidNotReceive().Confirm(Arg.Any<string>(), Arg.Any<bool>());
    }

    [Test]
    public async Task Run_DoesNotAddEnvironment_WhenConfirmationIsRejected()
    {
        var helper = Substitute.For<IOctopusHelper>();
        var lifeCycles = Substitute.For<ILifeCycleRepository>();
        var interaction = Substitute.For<ICommandInteraction>();
        helper.LifeCycles.Returns(lifeCycles);
        interaction.Confirm(Arg.Any<string>(), false).Returns(false);
        var runner = new EnvironmentToLifecycleRunner(helper, TestLanguageProvider.Create());

        var result = await runner.Run("Environments-1", "Lifecycles-1", "1", false, false, interaction);

        Assert.That(result, Is.EqualTo(0));
        interaction.Received(1).Confirm(Arg.Any<string>(), false);
        _ = lifeCycles.DidNotReceiveWithAnyArgs().AddEnvironmentToLifecyclePhase(default, default, default, default);
    }
}
