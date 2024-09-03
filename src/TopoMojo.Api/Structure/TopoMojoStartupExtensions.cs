// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

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
            Func<HypervisorServiceConfiguration> podConfig
        )
        {
            var config = podConfig();

            if (string.IsNullOrWhiteSpace(config.Url))
            {
                services.AddSingleton<IHypervisorService, TopoMojo.Hypervisor.vMock.MockHypervisorService>();
            }
            else
            {
                if (config.HypervisorType == HypervisorType.Proxmox)
                {
                    // give proxmox Random.Shared since it's not directly available in netstandard2.0
                    services.AddProxmoxHypervisor(Random.Shared);
                }
                else
                {
                    services.AddSingleton<IHypervisorService, TopoMojo.Hypervisor.vSphere.vSphereHypervisorService>();
                }
            }

            services.AddSingleton<HypervisorServiceConfiguration>(sp => config);
            services.AddHostedService<ServiceHostWrapper<IHypervisorService>>();

            return services;
        }
    }
}
