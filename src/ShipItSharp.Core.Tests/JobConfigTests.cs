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
            prioritise: true,
            machine: new Machine { Id = "Machines-1", Name = "web-01" });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.FallbackToDefaultChannel, Is.True);
        Assert.That(result.Value.ForceRedeploy, Is.True);
        Assert.That(result.Value.Prioritise, Is.True);
        Assert.That(result.Value.GroupFilter, Is.EqualTo("Payments"));
        Assert.That(result.Value.MachineId, Is.EqualTo("Machines-1"));
        Assert.That(result.Value.MachineName, Is.EqualTo("web-01"));
    }

    [Test]
    public void PromotionConfig_Create_PreservesPromotionOptions()
    {
        var result = PromotionConfig.Create(
            new EnvironmentModel { Id = "Environments-1", Name = "Test" },
            new EnvironmentModel { Id = "Environments-2", Name = "Dev" },
            "Payments",
            runningInteractively: true,
            prioritise: true,
            updateVariables: true,
            machine: new Machine { Id = "Machines-2", Name = "web-02" });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value.GroupFilter, Is.EqualTo("Payments"));
        Assert.That(result.Value.RunningInteractively, Is.True);
        Assert.That(result.Value.Prioritise, Is.True);
        Assert.That(result.Value.UpdateVariables, Is.True);
        Assert.That(result.Value.MachineId, Is.EqualTo("Machines-2"));
        Assert.That(result.Value.MachineName, Is.EqualTo("web-02"));
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
