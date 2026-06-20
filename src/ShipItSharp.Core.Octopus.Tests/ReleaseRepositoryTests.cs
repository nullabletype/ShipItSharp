using System;
using NUnit.Framework;
using ShipItSharp.Core.Octopus.Repositories;

namespace ShipItSharp.Core.Octopus.Tests;

[TestFixture]
public class ReleaseRepositoryTests
{
    [Test]
    public void BuildInferredReleaseName_UsesMajorMinorAndInteractiveSuffix()
    {
        var releaseName = ReleaseRepository.BuildInferredReleaseName("1.2.3", null, "Payments");

        Assert.That(releaseName, Is.EqualTo("1.2.i"));
    }

    [Test]
    public void BuildInferredReleaseName_AppendsChannelName()
    {
        var releaseName = ReleaseRepository.BuildInferredReleaseName("1.2.3-beta.4", "Beta", "Payments");

        Assert.That(releaseName, Is.EqualTo("1.2.i-Beta"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    [TestCase("1")]
    [TestCase("1.")]
    [TestCase(".2")]
    public void BuildInferredReleaseName_ThrowsClearError_WhenPackageVersionCannotBeInferred(string packageVersion)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ReleaseRepository.BuildInferredReleaseName(packageVersion, null, "Payments"));

        Assert.That(ex.Message, Does.Contain("Payments"));
    }
}
