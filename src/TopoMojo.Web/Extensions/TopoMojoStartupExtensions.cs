// Copyright 2020 Carnegie Mellon University. 
// Released under a MIT (SEI) license. See LICENSE.md in the project root. 

using System;
using TopoMojo.Abstractions;
using TopoMojo.Models;
using TopoMojo.Web.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TopoMojoStartupExtentions
    {
        public static IServiceCollection AddTopoMojoHypervisor(
            this IServiceCollection services,
            Func<HypervisorServiceConfiguration> podConfig
        )
        {
            var config = podConfig();

            if (string.IsNullOrWhiteSpace(config.Url))
            {
                services.AddSingleton<IHypervisorService, TopoMojo.vSphere.MockHypervisorService>();
            }
            else
            {
                services.AddSingleton<IHypervisorService, TopoMojo.vSphere.HypervisorService>();
            }

            services.AddSingleton<HypervisorServiceConfiguration>(sp => config);
            services.AddHostedService<ServiceHostWrapper<IHypervisorService>>();

            return services;
        }

    }
}
