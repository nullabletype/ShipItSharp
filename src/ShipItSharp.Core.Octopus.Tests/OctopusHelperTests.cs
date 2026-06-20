using System;
using System.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using Octopus.Client;
using ShipItSharp.Core.Octopus;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus.Tests;

[TestFixture]
public class OctopusHelperTests
{
    [TearDown]
    public void TearDown()
    {
        OctopusHelper.ClientCreator = endpoint => OctopusAsyncClient.Create(endpoint);
    }

    [Test]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => new OctopusHelper((IOctopusAsyncClient)null));
        Assert.That(ex.ParamName, Is.EqualTo("client"));
    }

    [Test]
    public void SetCacheImplementation_UsesProvidedCacheImplementation()
    {
        var oldCache = Substitute.For<ICacheObjects>();
        var newCache = Substitute.For<ICacheObjects>();
        var client = Substitute.For<IOctopusAsyncClient>();
        var helper = new OctopusHelper(client, oldCache);

        helper.SetCacheImplementation(newCache, 42);

        newCache.Received(1).SetCacheTimeout(42);
        oldCache.DidNotReceiveWithAnyArgs().SetCacheTimeout(default);
    }

    [Test]
    public void SetCacheImplementation_WithNullCache_FallsBackToNoCache()
    {
        var client = Substitute.For<IOctopusAsyncClient>();
        var helper = new OctopusHelper(client);

        Assert.DoesNotThrow(() => helper.SetCacheImplementation(null, 5));
        Assert.That(helper.CacheProvider, Is.Not.Null);
        Assert.That(helper.CacheProvider.GetType().Name, Is.EqualTo("NoCache"));
    }

    [Test]
    public async Task InitAsync_AssignsDefault_AndAppliesCacheTimeout()
    {
        var client = Substitute.For<IOctopusAsyncClient>();
        var cache = Substitute.For<ICacheObjects>();
        OctopusHelper.ClientCreator = _ => Task.FromResult(client);

        var helper = await OctopusHelper.InitAsync("https://example.octopus.app", "API-KEY", cache, 33);

        Assert.That(helper, Is.EqualTo(OctopusHelper.Default));
        cache.Received(1).SetCacheTimeout(33);
    }

    [Test]
    public void CreateAsync_WithInvalidInputs_ThrowsArgumentException()
    {
        Assert.ThrowsAsync<ArgumentException>(async () => await OctopusHelper.CreateAsync("", "abc"));
        Assert.ThrowsAsync<ArgumentException>(async () => await OctopusHelper.CreateAsync("https://example.octopus.app", ""));
    }
}
