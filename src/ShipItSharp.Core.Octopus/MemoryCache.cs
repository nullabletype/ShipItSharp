#region copyright
// /*
//     ShipItSharp Deployment Coordinator. Provides extra tooling to help
//     deploy software through Octopus Deploy.
// 
//     Copyright (C) 2022  Steven Davies
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>.
// */
#endregion


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
            _cache = cache;
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
            _cache.Set(CreateCacheKey<T>(key), value, cacheEntryOptions);
        }

        public T GetCachedObject<T>(string key)
        {
            if ((_cache == null))
            {
                return default(T);
            }

            if (_cache.TryGetValue(CreateCacheKey<T>(key), out T cachedValue))
            {
                return cachedValue;
            }
            return default(T);
        }

        private static string CreateCacheKey<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key must be provided.", nameof(key));
            }

            return $"{typeof(T).FullName}:{key}";
        }
    }
}
