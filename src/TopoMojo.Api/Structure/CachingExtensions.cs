// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using Microsoft.AspNetCore.DataProtection;
using StackExchange.Redis;
using TopoMojo.Api;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class CachingExtensions
    {
        public static IServiceCollection AddCache(this IServiceCollection services, Func<CacheOptions> configure = null)
        {
            var options = (configure != null)
                ? configure()
                : new CacheOptions();

            services.AddMemoryCache();

            services.AddSingleton(_ => options);

            if (string.IsNullOrWhiteSpace(options?.RedisUrl))
            {
                services.AddDistributedMemoryCache();
            }
            else
            {
                services.AddStackExchangeRedisCache(opt =>
                {
                    opt.Configuration = options.RedisUrl;
                    opt.InstanceName = options.Key;
                });
            }

            return services;
        }

        public static IDataProtectionBuilder PersistKeys(this IDataProtectionBuilder builder, Func<CacheOptions> configure = null)
        {
            var options = (configure != null)
                ? configure()
                : new CacheOptions();

            if (string.IsNullOrWhiteSpace(options?.RedisUrl))
            {
                builder.PersistKeysToFileSystem(
                    new DirectoryInfo(Path.Combine(options.SharedFolder, options.DataProtectionFolder))
                );
            }
            else
            {
                builder.PersistKeysToStackExchangeRedis(
                    ConnectionMultiplexer.Connect(options.RedisUrl),
                    $"{options.Key}-dpk"
                );
            }

            return builder;
        }
    }
}
