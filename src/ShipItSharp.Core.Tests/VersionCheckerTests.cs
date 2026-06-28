using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.Configuration.Interfaces;
using ShipItSharp.Core.VersionChecking;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class VersionCheckerTests
{
    [Test]
    public async Task GetLatestVersion_ReturnsNoUpdate_WhenProviderReturnsNull()
    {
        var provider = Substitute.For<IVersionCheckingProvider>();
        provider.GetLatestRelease(false).Returns(Task.FromResult<IRelease>(null));
        var checker = new VersionChecker(provider);

        var result = await checker.GetLatestVersion();

        Assert.That(result.NewVersion, Is.False);
        Assert.That(result.Release, Is.Null);
    }

    [Test]
    public async Task GetLatestVersion_SetsCurrentVersion_OnProviderRelease()
    {
        var provider = Substitute.For<IVersionCheckingProvider>();
        var release = Substitute.For<IRelease>();
        release.TagName.Returns("999.0.0");
        provider.GetLatestRelease(false).Returns(release);
        var checker = new VersionChecker(provider);

        var result = await checker.GetLatestVersion();

        Assert.That(result.NewVersion, Is.True);
        Assert.That(result.Release, Is.SameAs(release));
        Assert.That(release.CurrentVersion, Is.Not.Empty);
    }

    [Test]
    public async Task GetLatestVersion_DoesNotRequestPreReleases_ByDefault()
    {
        var provider = Substitute.For<IVersionCheckingProvider>();
        provider.GetLatestRelease(false).Returns(Task.FromResult<IRelease>(null));
        var checker = new VersionChecker(provider);

        await checker.GetLatestVersion();

        await provider.Received(1).GetLatestRelease(false);
    }

    [Test]
    public async Task GetLatestVersion_RequestsPreReleases_WhenConfigEnablesBetaChecks()
    {
        var provider = Substitute.For<IVersionCheckingProvider>();
        var configuration = Substitute.For<IConfiguration>();
        configuration.CheckForBetaReleases.Returns(true);
        provider.GetLatestRelease(true).Returns(Task.FromResult<IRelease>(null));
        var checker = new VersionChecker(provider, configuration);

        await checker.GetLatestVersion();

        await provider.Received(1).GetLatestRelease(true);
    }

    [Test]
    public async Task GetLatestVersion_HandlesPreReleaseTagSuffixes()
    {
        var provider = Substitute.For<IVersionCheckingProvider>();
        var configuration = Substitute.For<IConfiguration>();
        var release = Substitute.For<IRelease>();
        configuration.CheckForBetaReleases.Returns(true);
        release.TagName.Returns("999.0.0-beta.1");
        provider.GetLatestRelease(true).Returns(release);
        var checker = new VersionChecker(provider, configuration);

        var result = await checker.GetLatestVersion();

        Assert.That(result.NewVersion, Is.True);
        Assert.That(result.Release, Is.SameAs(release));
    }
}
