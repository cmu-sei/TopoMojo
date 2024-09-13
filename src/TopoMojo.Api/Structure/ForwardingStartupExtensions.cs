// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using TopoMojo.Api;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ForwardingStartupExtensions
    {

        public static IServiceCollection ConfigureForwarding(
            this IServiceCollection services,
            ForwardHeaderOptions options
        )
        {
            services.Configure<ForwardedHeadersOptions>(config => {

                if (Enum.TryParse<ForwardedHeaders>(
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
                            new char[] { ' ', ','},
                            StringSplitOptions.RemoveEmptyEntries
                        )
                    )
                    {
                        string[] net = item.Split('/');

                        if (IPAddress.TryParse(net.First(), out IPAddress ipaddr)
                            && Int32.TryParse(net.Last(), out int prefix)
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
                            new char[] { ' ', ','},
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
