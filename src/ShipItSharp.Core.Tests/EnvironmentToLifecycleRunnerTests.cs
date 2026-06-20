using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.JobRunners;
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

        var result = await runner.Run("Environments-1", "Lifecycles-1", "abc", false);

        Assert.That(result, Is.EqualTo(-1));
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

        var result = await runner.Run("Environments-1", "Lifecycles-1", "2", true);

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

        var result = await runner.Run("Environments-1", "Lifecycles-1", "1", false);

        Assert.That(result, Is.EqualTo(0));
    }
}
