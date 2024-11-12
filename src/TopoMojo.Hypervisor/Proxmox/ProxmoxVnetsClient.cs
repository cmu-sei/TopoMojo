// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Corsinvest.ProxmoxVE.Api;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor.Proxmox.Models;

namespace TopoMojo.Hypervisor.Proxmox
{
    public interface IProxmoxVnetsClient
    {
        Task<IEnumerable<PveVnet>> CreateVnets(IEnumerable<CreatePveVnet> createVnets);
        Task<IEnumerable<PveVnet>> DeleteVnets(IEnumerable<string> names);
        Task<IEnumerable<PveVnet>> DeleteVnetsByTerm(string term);
        Task<IEnumerable<PveVnet>> GetVnets();
        Task ReloadVnets();
    }

    public class ProxmoxVnetsClient : IProxmoxVnetsClient
    {
        private readonly ILogger<ProxmoxVnetsClient> _logger;
        private readonly PveClient _pveClient;
        private readonly Random _random;
        private readonly string _sdnZone;

        public ProxmoxVnetsClient
        (
            HypervisorServiceConfiguration hypervisorOptions,
            ILogger<ProxmoxVnetsClient> logger,
            Random random
        )
        {
            _logger = logger;

            int port = 443;
            string host = hypervisorOptions.Url;
            if (Uri.TryCreate(hypervisorOptions.Url, UriKind.RelativeOrAbsolute, out Uri result) && result.IsAbsoluteUri)
            {
                host = result.Host;
                port = result.Port;
            }
            hypervisorOptions.Host = host;

            _pveClient = new PveClient(host, port)
            {
                ApiToken = hypervisorOptions.AccessToken
            };
            _random = random;
            _sdnZone = hypervisorOptions.SDNZone;
        }

        public async Task<IEnumerable<PveVnet>> CreateVnets(IEnumerable<CreatePveVnet> createVnets)
        {
            var existingNets = await GetVnets();
            var deployedNets = new List<PveVnet>();

            foreach (var createVnet in createVnets)
            {
                if (existingNets.Any(n => n.Alias == createVnet.Alias))
                {
                    _logger.LogDebug("Skipped creating vnet {vnet} - it already exists.", createVnet);
                    continue;
                }

                var newVnetTag = createVnet.Tag;

                if (newVnetTag == null)
                {
                    do
                    {
                        // VXLAN range should be 1 - 16777215, but some devices may reserve 1-4096
                        newVnetTag = _random.Next(4097, 16777215);
                    }
                    while (existingNets.Any(n => n.Tag == newVnetTag));
                }

                var vnetId = GetRandomVnetId();

                // check for existence of alias = vnetname--gamespaceid
                var createTask = await _pveClient.Cluster.Sdn.Vnets.Create
                (
                    vnet: vnetId,
                    tag: newVnetTag,
                    zone: createVnet.Zone,
                    alias: createVnet.Alias
                );

                if (createTask.IsSuccessStatusCode)
                {

                    deployedNets.Add
                    (
                        new PveVnet
                        {
                            Alias = createVnet.Alias,
                            Tag = newVnetTag.GetValueOrDefault(),
                            Type = string.Empty,
                            Vnet = vnetId,
                            Zone = createVnet.Zone
                        }
                    );
                }
            }

            return deployedNets;
        }

        public async Task<IEnumerable<PveVnet>> DeleteVnets(IEnumerable<string> aliases)
        {
            _logger.LogDebug("Deleting vnets: {aliases}", string.Join(",", aliases));
            var vnets = await GetVnets();
            var deletedPveNets = new List<PveVnet>();

            foreach (var alias in aliases)
            {
                var vnet = vnets.SingleOrDefault(v => v.Alias == alias);

                if (vnet != null)
                {
                    var deleteTask = await _pveClient.Cluster.Sdn.Vnets[vnet.Vnet].Delete();
                    await _pveClient.WaitForTaskToFinish(deleteTask);

                    if (deleteTask.IsSuccessStatusCode)
                    {
                        deletedPveNets.Add(vnet);
                    }
                }
                else
                {
                    _logger.LogDebug("Vnet delete requested for {alias}, but the network doesn't exist.", alias);
                }
            }

            return deletedPveNets;
        }

        public async Task<IEnumerable<PveVnet>> DeleteVnetsByTerm(string term)
        {
            var vnets = await GetVnets();
            var deletedPveNets = new List<PveVnet>();

            foreach (var vnet in vnets.Where(x => x.Alias.Contains(term)))
            {
                var deleteTask = await _pveClient.Cluster.Sdn.Vnets[vnet.Vnet].Delete();
                await _pveClient.WaitForTaskToFinish(deleteTask);

                if (deleteTask.IsSuccessStatusCode)
                {
                    deletedPveNets.Add(vnet);
                }
            }

            return deletedPveNets;
        }

        public async Task<IEnumerable<PveVnet>> GetVnets()
        {
            var task = await _pveClient.Cluster.Sdn.Vnets.Index();
            await _pveClient.WaitForTaskToFinish(task);

            if (!task.IsSuccessStatusCode)
                throw new Exception($"Failed to load virtual networks from Proxmox. Status code: {task.StatusCode}");

            return task.ToModel<PveVnet[]>().Where(x => x.Zone == _sdnZone);
        }

        public async Task ReloadVnets()
        {
            var reloadTask = await _pveClient.Cluster.Sdn.Reload();
            await _pveClient.WaitForTaskToFinish(reloadTask);
        }

        private string GetRandomVnetId()
        {
            var builder = new StringBuilder();
            var _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

            for (int i = 0; i <= 7; i++)
            {
                builder.Append(_chars[_random.Next(_chars.Length)]);
            }

            return builder.ToString();
        }
    }
}
