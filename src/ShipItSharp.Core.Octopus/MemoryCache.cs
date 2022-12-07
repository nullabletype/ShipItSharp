using System;
using Microsoft.Extensions.Caching.Memory;
using ShipItSharp.Core.Octopus.Interfaces;

namespace ShipItSharp.Core.Octopus
{
    public class MemoryCache : ICacheObjects
    {
        private readonly IMemoryCache _cache;
        private int _cacheTimeout = 20;

        public MemoryCache(IMemoryCache cache)
        {
            this._cache = cache;
        }

        public void SetCacheTimeout(int cacheTimeoutToSet = 1)
        {
            _cacheTimeout = cacheTimeoutToSet;
            if (cacheTimeoutToSet < 1)
            {
                _cacheTimeout = 1;
            }
        }

        public void CacheObject<T>(string key, T value)
        {
            if (_cache == null)
            {
                return;
            }
            var cacheEntryOptions = new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(_cacheTimeout));
            _cache.Set(key + typeof(T).Name, value, cacheEntryOptions);
        }

        public T GetCachedObject<T>(string key)
        {
            if ((_cache != null) && _cache.TryGetValue(key + typeof(T).Name, out T cachedValue))
            {
                return cachedValue;
            }
            return default(T);
        }
    }
}