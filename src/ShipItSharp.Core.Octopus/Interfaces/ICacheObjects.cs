using Microsoft.Extensions.Caching.Memory;

namespace ShipItSharp.Core.Octopus.Interfaces;

public interface ICacheObjects
{
    void SetCacheTimeout(int cacheTimeoutToSet = 1);
    void CacheObject<T>(string key, T value);
    T GetCachedObject<T>(string key);
}