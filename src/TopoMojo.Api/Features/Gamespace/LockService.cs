// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;

namespace TopoMojo.Api.Services
{
    public interface ILockService
    {
        Task<bool> Lock(string key);
        Task Unlock(string key, Exception ex = null);
    }

    public class CacheLockService : ILockService
    {
        private const string prefix = ":lck:";
        private const int delay = 100;
        private readonly IDistributedCache _cache;
        private readonly DistributedCacheEntryOptions _opts;

        public CacheLockService(
            IDistributedCache cache
        ) {
            _cache = cache;

            _opts = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = new TimeSpan(0, 0, 60)
            };
        }

        public async Task<bool> Lock(string key)
        {
            key = $"{prefix}{key}";

            string actual = await _cache.GetStringAsync(key);

            if (!string.IsNullOrEmpty(actual))
                return false;

            string expected = Guid.NewGuid().ToString("n");

            await _cache.SetStringAsync(key, expected, _opts);

            actual = await _cache.GetStringAsync(key);

            return actual.Equals(expected);
        }

        public Task Unlock(string key, Exception ex = null)
        {
            _cache.RemoveAsync($"{prefix}{key}").Wait();

            if (ex is Exception)
                throw ex;

            return Task.CompletedTask;
        }
    }

}
