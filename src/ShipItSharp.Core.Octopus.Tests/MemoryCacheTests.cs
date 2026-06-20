using System;
using Microsoft.Extensions.Caching.Memory;
using NUnit.Framework;
using FrameworkMemoryCache = Microsoft.Extensions.Caching.Memory.MemoryCache;
using OctopusMemoryCache = ShipItSharp.Core.Octopus.MemoryCache;

namespace ShipItSharp.Core.Octopus.Tests;

[TestFixture]
public class MemoryCacheTests
{
    [Test]
    public void CacheObject_SeparatesValuesByType()
    {
        var cache = new OctopusMemoryCache(new FrameworkMemoryCache(new MemoryCacheOptions()));

        cache.CacheObject("same-key", "text");
        cache.CacheObject("same-key", 42);

        Assert.That(cache.GetCachedObject<string>("same-key"), Is.EqualTo("text"));
        Assert.That(cache.GetCachedObject<int>("same-key"), Is.EqualTo(42));
    }

    [Test]
    public void CacheObject_RejectsMissingKey()
    {
        var cache = new OctopusMemoryCache(new FrameworkMemoryCache(new MemoryCacheOptions()));

        Assert.Throws<ArgumentException>(() => cache.CacheObject<string>("", "value"));
        Assert.Throws<ArgumentException>(() => cache.GetCachedObject<string>(" "));
    }

    [Test]
    public void SetCacheTimeout_UsesMinimumOfOneSecond()
    {
        var cache = new OctopusMemoryCache(new FrameworkMemoryCache(new MemoryCacheOptions()));

        cache.SetCacheTimeout(0);

        Assert.DoesNotThrow(() => cache.CacheObject("key", "value"));
        Assert.That(cache.GetCachedObject<string>("key"), Is.EqualTo("value"));
    }
}
