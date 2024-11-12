// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using TopoMojo.Api;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ForwardingStartupExtensions
    {
        private static readonly char[] separator = [' ', ','];

        public static IServiceCollection ConfigureForwarding(
            this IServiceCollection services,
            ForwardHeaderOptions options
        )
        {
            services.Configure<ForwardedHeadersOptions>(config => {

                if (Enum.TryParse(
                    options.TargetHeaders ?? "None",
                    true,
                    out ForwardedHeaders targets)
                )
                {
                    config.ForwardedHeaders = targets;
                }

                config.ForwardLimit = options.ForwardLimit;

                if (options.ForwardLimit == 0)
                {
                    config.ForwardLimit = null;
                }

                if (!string.IsNullOrEmpty(options.KnownNetworks))
                {
                    foreach (string item in options.KnownNetworks
                        .Split(
                            separator,
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    )
                    {
                        string[] net = item.Split('/');

                        if (IPAddress.TryParse(net.First(), out IPAddress ipaddr)
                            && int.TryParse(net.Last(), out int prefix)
                        )
                        {
                            config.KnownNetworks.Add(new AspNetCore.HttpOverrides.IPNetwork(ipaddr, prefix));
                        }
                    }
                }

                if (!string.IsNullOrEmpty(options.KnownProxies))
                {
                    foreach (string ip in options.KnownProxies
                        .Split(
                            separator,
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    )
                    {
                        if (IPAddress.TryParse(ip, out IPAddress ipaddr))
                        {
                            config.KnownProxies.Add(ipaddr);
                        }
                    }
                }

                if (!string.IsNullOrEmpty(options.ForwardedForHeaderName))
                {
                    config.ForwardedForHeaderName = options.ForwardedForHeaderName;
                }

            });

            return services;
        }
    }
}
