using System;
using Microsoft.Extensions.Caching.Memory;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus;

public class MemoryCache : ICacheObjects
{
    private IMemoryCache cache;
    private int cacheTimeout = 20;

    public MemoryCache(IMemoryCache cache)
    {
        this.cache = cache;
    }

    public void SetCacheTimeout(int cacheTimeoutToSet = 1)
    {
        cacheTimeout = cacheTimeoutToSet;
        if (cacheTimeoutToSet < 1)
        {
            cacheTimeout = 1;
        }
    }

    public void CacheObject<T>(string key, T value)
    {
        if(cache == null)
        {
            return;
        }
        var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(cacheTimeout));
        cache.Set(key + typeof(T).Name, value, cacheEntryOptions);
    }
    
    public T GetCachedObject<T>(string key)
    {
        if (cache != null && cache.TryGetValue(key + typeof(T).Name, out T cachedValue))
        {
            return cachedValue;
        }
        return default(T);
    }
}