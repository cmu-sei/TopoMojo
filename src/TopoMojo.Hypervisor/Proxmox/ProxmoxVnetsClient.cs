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

        public ProxmoxVnetsClient
        (
            HypervisorServiceConfiguration hypervisorOptions,
            ILogger<ProxmoxVnetsClient> logger,
            Random random
        )
        {
            _logger = logger;
            _pveClient = new PveClient(hypervisorOptions.Host, 443)
            {
                ApiToken = hypervisorOptions.AccessToken
            };
            _random = random;
        }

        public async Task<IEnumerable<PveVnet>> CreateVnets(IEnumerable<CreatePveVnet> createVnets)
        {
            var existingNets = await this.GetVnets();
            var deployedNets = new List<PveVnet>();

            foreach (var createVnet in createVnets)
            {
                if (existingNets.Any(n => n.Alias == createVnet.Alias))
                {
                    _logger.LogDebug($"Skipped creating vnet {createVnet} - it already exists.");
                    continue;
                }

                var newVnetTag = createVnet.Tag;

                if (newVnetTag == null)
                {
                    do
                    {
                        newVnetTag = _random.Next(100, 100000);
                    }
                    while (existingNets.Any(n => n.Tag == newVnetTag));
                }

                var vnetId = this.GetRandomVnetId();

                // check for existence of alias = vnetname--gamespaceid
                var createTask = _pveClient.Cluster.Sdn.Vnets.Create
                (
                    vnet: vnetId,
                    tag: newVnetTag,
                    zone: createVnet.Zone,
                    alias: createVnet.Alias
                ).Result;

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
            var vnets = await this.GetVnets();
            var deletedPveNets = new List<PveVnet>();

            foreach (var alias in aliases)
            {
                var vnet = vnets.SingleOrDefault(v => v.Alias == alias);

                if (vnet != null)
                {
                    var deleteTask = await _pveClient.Cluster.Sdn.Vnets[vnet.Vnet].Delete();
                    await this.WaitForProxmoxTask(deleteTask);

                    if (deleteTask.IsSuccessStatusCode)
                    {
                        deletedPveNets.Add(vnet);
                    }
                }
                else
                {
                    _logger.LogDebug($"Vnet delete requested for {alias}, but the network doesn't exist.");
                }
            }

            return deletedPveNets;
        }

        public async Task<IEnumerable<PveVnet>> DeleteVnetsByTerm(string term)
        {
            var vnets = await this.GetVnets();
            var deletedPveNets = new List<PveVnet>();

            foreach (var vnet in vnets.Where(x => x.Alias.Contains(term)))
            {
                var deleteTask = await _pveClient.Cluster.Sdn.Vnets[vnet.Vnet].Delete();
                await this.WaitForProxmoxTask(deleteTask);

                if (deleteTask.IsSuccessStatusCode)
                {
                    deletedPveNets.Add(vnet);
                }
            }

            return deletedPveNets;
        }

        public async Task<IEnumerable<PveVnet>> GetVnets()
        {
            var task = _pveClient.Cluster.Sdn.Vnets.Index().Result;
            await WaitForProxmoxTask(task);

            if (!task.IsSuccessStatusCode)
                throw new Exception($"Failed to load virtual networks from Proxmox. Status code: {task.StatusCode}");

            return task.ToModel<PveVnet[]>();
        }

        public async Task ReloadVnets()
        {
            var reloadTask = _pveClient.Cluster.Sdn.Reload().Result;
            await _pveClient.WaitForTaskToFinishAsync(reloadTask);
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

        private async Task WaitForProxmoxTask(Result proxmoxTask)
        {
            try
            {
                await _pveClient.WaitForTaskToFinishAsync(proxmoxTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for task to finish");
            }
        }
    }
}
