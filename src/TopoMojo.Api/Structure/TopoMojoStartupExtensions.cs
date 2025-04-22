// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System;
using System.Linq;
using System.Reflection;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api;
using TopoMojo.Api.Data;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor.Proxmox;
using TopoMojo.Hypervisor.vMock;
using TopoMojo.Hypervisor.vSphere;
using VimClient;
using TopoMojo.Hypervisor.Meta;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TopoMojoStartupExtentions
    {

        public static IServiceCollection AddTopoMojo(
            this IServiceCollection services,
            CoreOptions options
        )
        {

            services.AddSingleton<CoreOptions>(_ => options);

            // Auto-discover from EntityService pattern
            foreach (var t in Assembly
                .GetExecutingAssembly()
                .ExportedTypes
                .Where(t => t.Namespace == "TopoMojo.Api.Services"
                    && t.Name.EndsWith("Service")
                    && t.IsClass
                    && !t.IsAbstract
                )
            )
            {
                foreach (Type i in t.GetInterfaces())
                    services.AddScoped(i, t);
                services.AddScoped(t);
            }

            foreach (var t in Assembly
                .GetExecutingAssembly()
                .ExportedTypes
                .Where(t => t.Namespace == "TopoMojo.Api.Validators"
                    && t.Name.EndsWith("Validator")
                    && t.IsClass
                    && !t.IsAbstract
                )
            )
            {
                foreach (Type i in t.GetInterfaces())
                    services.AddScoped(i, t);
                services.AddScoped(t);
            }

            return services;
        }

        public static IMapperConfigurationExpression AddTopoMojoMaps(
            this IMapperConfigurationExpression cfg
        )
        {
            cfg.AddMaps(Assembly.GetExecutingAssembly());
            return cfg;
        }

        public static IServiceCollection AddTopoMojoData(
            this IServiceCollection services,
            string provider,
            string connstr,
            string migrationAssembly = null
        )
        {

            if (string.IsNullOrEmpty(migrationAssembly))
                migrationAssembly = Assembly.GetExecutingAssembly().GetName().Name;

            switch (provider.ToLower())
            {

                case "sqlserver":
                    // builder.Services.AddEntityFrameworkSqlServer();
                    services.AddDbContext<TopoMojoDbContext, TopoMojoDbContextSqlServer>(
                        db => db.UseSqlServer(connstr, options => options.MigrationsAssembly(migrationAssembly))
                    );
                    break;

                case "postgresql":
                    // services.AddEntityFrameworkNpgsql();
                    services.AddDbContext<TopoMojoDbContext, TopoMojoDbContextPostgreSQL>(
                        db => db.UseNpgsql(connstr, options => options.MigrationsAssembly(migrationAssembly))
                    );
                    break;

                default:
                    // services.AddEntityFrameworkInMemoryDatabase();
                    services.AddDbContext<TopoMojoDbContext, TopoMojoDbContextInMemory>(
                        db => db.UseInMemoryDatabase(connstr)
                    );
                    break;

            }

            // Auto-discover from EntityStore and IEntityStore pattern
            foreach (var t in Assembly
                .GetExecutingAssembly()
                .ExportedTypes
                .Where(t =>
                    t.Namespace == "TopoMojo.Api.Data"
                    && t.Name.EndsWith("Store")
                    && t.IsClass
                    && !t.IsAbstract
                )
            )
            {
                foreach (Type i in t.GetInterfaces())
                    services.AddScoped(i, t);
                services.AddScoped(t);
            }

            return services;
        }

        public static IServiceCollection AddTopoMojoHypervisor(
            this IServiceCollection services,
            Func<HypervisorServiceConfiguration[]> podConfigs
        )
        {
            var configs = podConfigs();
            var registeredTypes = new List<Type>();

            foreach (var config in configs)
            {
                Type type;

                if (string.IsNullOrWhiteSpace(config.Url))
                {
                    type = typeof(MockHypervisorService);
                }
                else if (config.HypervisorType == HypervisorType.Proxmox)
                {
                    type = typeof(ProxmoxHypervisorService);
                }
                else
                {
                    type = typeof(VSphereHypervisorService);
                }

                if (type is not null)
                {
                    if (registeredTypes.Contains(type))
                    {
                        throw new ArgumentException($"A Hypervisor of type {type} has already been registered. You may only specify a maximum of one of each type.");
                    }
                    else if (type == typeof(ProxmoxHypervisorService))
                    {
                        // give proxmox Random.Shared since it's not directly available in netstandard2.0
                        services.AddProxmoxHypervisor(config, Random.Shared);
                    }
                    else
                    {
                        services.AddSingleton(type, (sp) => ActivatorUtilities.CreateInstance(sp, type, config));
                    }

                    registeredTypes.Add(type);
                }

                // services.AddSingleton<HypervisorServiceConfiguration>(sp => config);
                //var wrapperType = typeof(ServiceHostWrapper<>).MakeGenericType(type);
                if (typeof(IHostedService).IsAssignableFrom(type))
                {
                    services.AddSingleton(typeof(IHostedService), sp => sp.GetRequiredService(type));
                }
            }

            if (registeredTypes.Count == 0)
            {
                throw new ArgumentException("No Hypervisor types registered");
            }
            else if (registeredTypes.Count > 1)
            {
                services.AddSingleton<IHypervisorService, MetaHypervisorService>((sp) =>
                {
                    var hypervisorServices = new List<IHypervisorService>();

                    foreach (var type in registeredTypes)
                    {
                        hypervisorServices.Add(sp.GetRequiredService(type) as IHypervisorService);
                    }

                    return ActivatorUtilities.CreateInstance<MetaHypervisorService>(sp, (object)hypervisorServices.ToArray());
                });
            }
            else
            {
                services.AddSingleton(typeof(IHypervisorService), sp => sp.GetRequiredService(registeredTypes.First()));
            }

            return services;
        }
    }
}
