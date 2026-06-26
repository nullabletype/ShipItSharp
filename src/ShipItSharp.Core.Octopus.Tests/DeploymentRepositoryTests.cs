using NUnit.Framework;
using Octopus.Client.Model;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Octopus.Tests;

[TestFixture]
public class DeploymentRepositoryTests
{
    [Test]
    public void ApplySpecificMachine_InitializesSpecificMachineIds_WhenOctopusModelLeavesItNull()
    {
        var deployment = new DeploymentResource();

        DeploymentRepository.ApplySpecificMachine(deployment, "Machines-1");

        Assert.That(deployment.SpecificMachineIds, Is.Not.Null);
        Assert.That(deployment.SpecificMachineIds, Does.Contain("Machines-1"));
    }
}
