namespace ShipItSharp.Core.Octopus.Interfaces;

class NoCache : ICacheObjects
{
    public void SetCacheTimeout(int cacheTimeoutToSet = 1) { }

    public void CacheObject<T>(string key, T value) { }

    public T GetCachedObject<T>(string key)
    {
        return default;
    }
}