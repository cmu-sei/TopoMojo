using System;
using System.Linq;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Node;
using Microsoft.Extensions.DependencyInjection;

namespace TopoMojo.Hypervisor.Proxmox
{
    public static class ProxmoxExtensions
    {
        public static long GetId(this Vm vm)
        {
            return long.Parse(vm.Id);
        }

        public static string GetParentFilename(this NodeStorageContent content)
        {
            if (string.IsNullOrEmpty(content.Parent))
            {
                return null;
            }

            // raw format parent: base-<vmId>-disk-<disk_num>@__base__
            if (content.Parent.Contains('@'))
            {
                // return e.g. base-100-disk-0
                return content.Parent.Split('@')[0];
            }
            // qcow2 format: ../<vmId>/base-<vmId>-disk-<disk_num>.qcow2
            else if (content.Parent.StartsWith("../"))
            {
                // return e.g. 100/base-100-disk0.qcow2
                return content.Parent.Split(new[] { '/' }, 2)[1];
            }
            else
            {
                throw new InvalidOperationException("Unsupported NodeStorageContent type");
            }
        }

        /// <summary>
        /// Adds support for the Proxmox hypervisor to Topomojo.
        /// </summary>
        /// <param name="services">The app's service collection.</param>
        /// <param name="random">
        ///     An instance of Random which will be used across the hypervisor's implementation. Where available,
        ///     The thread-safe Random.Shared instance is recommended. If no instance is supplied, a default
        ///     will be created.
        /// </param>
        /// <returns></returns>
        public static IServiceCollection AddProxmoxHypervisor(this IServiceCollection services, Random random = null)
        {
            return services
                .AddSingleton<IHypervisorService, ProxmoxHypervisorService>()
                .AddSingleton<IProxmoxNameService, ProxmoxNameService>()
                .AddSingleton<IProxmoxVlanManager, ProxmoxVlanManager>()
                .AddSingleton<IProxmoxVnetsClient, ProxmoxVnetsClient>()
                .AddSingleton(_ => random ?? new Random());
        }
    }
}
