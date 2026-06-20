using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using ShipItSharp.Core.VersionChecking;

namespace ShipItSharp.Core.Tests;

[TestFixture]
public class VersionCheckerTests
{
    [Test]
    public async Task GetLatestVersion_ReturnsNoUpdate_WhenProviderReturnsNull()
    {
        var provider = Substitute.For<IVersionCheckingProvider>();
        provider.GetLatestRelease().Returns(Task.FromResult<IRelease>(null));
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
        provider.GetLatestRelease().Returns(release);
        var checker = new VersionChecker(provider);

        var result = await checker.GetLatestVersion();

        Assert.That(result.NewVersion, Is.True);
        Assert.That(result.Release, Is.SameAs(release));
        Assert.That(release.CurrentVersion, Is.Not.Empty);
    }
}
