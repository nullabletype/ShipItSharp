using System;
using System.IO;
using NUnit.Framework;
using ShipItSharp.Core.Deployment.Models;
using ShipItSharp.Core.JobRunners.JobConfigs;
using EnvironmentModel = ShipItSharp.Core.Deployment.Models.Environment;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class JobConfigTests
{
    [Test]
    public void DeployConfig_Create_Fails_WhenEnvironmentIsMissing()
    {
        var result = DeployConfig.Create(null, "Default", null, null, null, runningInteractively: false);

        Assert.That(result.IsFailure, Is.True);
    }

    [Test]
    public void DeployConfig_Create_Fails_WhenChannelIsMissing()
    {
        var result = DeployConfig.Create(new EnvironmentModel { Id = "Environments-1" }, "", null, null, null, runningInteractively: false);

        Assert.That(result.IsFailure, Is.True);
    }

    [Test]
    public void DeployConfig_Create_PreservesDeploymentOptions()
    {
        var savePath = Path.Combine(Path.GetTempPath(), "shipitsharp-profile-" + Guid.NewGuid() + ".json");

        var result = DeployConfig.Create(
            new EnvironmentModel { Id = "Environments-1", Name = "Prod" },
            "Default",
            "Fallback",
            "Payments",
            savePath,
            runningInteractively: true,
            forceRedeploy: true,
            prioritise: true);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.FallbackToDefaultChannel, Is.True);
        Assert.That(result.Value.ForceRedeploy, Is.True);
        Assert.That(result.Value.Prioritise, Is.True);
        Assert.That(result.Value.GroupFilter, Is.EqualTo("Payments"));
    }

    [Test]
    public void DeploySpecificConfig_Create_RequiresReleaseName()
    {
        var result = DeploySpecificConfig.Create(new EnvironmentModel { Id = "Environments-1" }, "", null, false, null);

        Assert.That(result.IsFailure, Is.True);
    }

    [Test]
    public void PromotionConfig_Create_RequiresSourceAndDestinationEnvironments()
    {
        var missingSource = PromotionConfig.Create(new EnvironmentModel { Id = "Environments-1" }, null, null, false);
        var missingDestination = PromotionConfig.Create(null, new EnvironmentModel { Id = "Environments-2" }, null, false);

        Assert.That(missingSource.IsFailure, Is.True);
        Assert.That(missingDestination.IsFailure, Is.True);
    }
}
