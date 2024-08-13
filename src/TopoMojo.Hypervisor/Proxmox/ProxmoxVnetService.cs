using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Corsinvest.ProxmoxVE.Api;
using TopoMojo.Hypervisor.Common;
using TopoMojo.Hypervisor.Proxmox.Models;

namespace TopoMojo.Hypervisor.Proxmox
{
    public interface IProxmoxVnetService
    {
        Task<IEnumerable<PveVnet>> Deploy(IEnumerable<string> vnetNames, CancellationToken cancellationToken);
    }

    public class ProxmoxVnetService : IProxmoxVnetService
    {
        private readonly static Lazy<SemaphoreSlim> DEPLOY_SEMAPHORE = new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(1));
        private readonly static Lazy<DebounceAddCollection<string>> VNET_DEPLOY_NAMES = new Lazy<DebounceAddCollection<string>>(() => new DebounceAddCollection<string>(300));

        private readonly HypervisorServiceConfiguration _hypervisorOptions;
        private readonly IProxmoxNameService _nameService;
        private readonly PveClient _pveClient;
        private readonly Random _random;

        public ProxmoxVnetService
        (
            HypervisorServiceConfiguration hypervisorOptions,
            IProxmoxNameService nameService,
            Random random
        )
        {
            _hypervisorOptions = hypervisorOptions;
            _nameService = nameService;
            _pveClient = new PveClient(hypervisorOptions.Host, 443) { ApiToken = hypervisorOptions.Password };
            _random = random;
        }

        public async Task<IEnumerable<PveVnet>> Deploy(IEnumerable<string> vnetNames, CancellationToken cancellationToken)
        {
            var debouncedVnetNames = await VNET_DEPLOY_NAMES.Value.AddRange(vnetNames, CancellationToken.None);

            try
            {
                await DEPLOY_SEMAPHORE.Value.WaitAsync(cancellationToken);

                var task = _pveClient.Cluster.Sdn.Vnets.Index().Result;
                var hostNets = task.ToModel<PveVnet[]>();
                var newNets = debouncedVnetNames.Where(vnetName => !hostNets.Any(n => n.Alias == _nameService.ToPveName(vnetName))).Distinct();
                var deployedVNets = new List<PveVnet>();

                foreach (var vnetName in newNets)
                {
                    var newVnetTag = default(int?);

                    do
                    {
                        newVnetTag = _random.Next(100, 100000);
                    }
                    while (hostNets.Any(n => n.Tag == newVnetTag));

                    var vnetId = this.GetRandomVnetId();
                    var pveName = _nameService.ToPveName(vnetName);

                    var createTask = _pveClient.Cluster.Sdn.Vnets.Create
                    (
                        vnet: vnetId,
                        tag: newVnetTag,
                        zone: _hypervisorOptions.SDNZone,
                        alias: pveName
                    ).Result;

                    if (createTask.IsSuccessStatusCode)
                    {
                        deployedVNets.Add(new PveVnet
                        {
                            Alias = pveName,
                            Tag = newVnetTag.GetValueOrDefault(),
                            Type = string.Empty,
                            Vnet = vnetId,
                            Zone = _hypervisorOptions.SDNZone
                        });
                    }
                }

                if (deployedVNets.Any())
                {
                    var reloadTask = _pveClient.Cluster.Sdn.Reload().Result;
                    await _pveClient.WaitForTaskToFinishAsync(reloadTask);
                }

                return deployedVNets;
            }
            finally
            {
                VNET_DEPLOY_NAMES.Value.Clear();
                DEPLOY_SEMAPHORE.Value.Release();
            }
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
