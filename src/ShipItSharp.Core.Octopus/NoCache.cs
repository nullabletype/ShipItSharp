using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus
{
    internal class NoCache : ICacheObjects
    {
        public void SetCacheTimeout(int cacheTimeoutToSet = 1) { }

        public void CacheObject<T>(string key, T value) { }

        public T GetCachedObject<T>(string key)
        {
            return default(T);
        }
    }
}