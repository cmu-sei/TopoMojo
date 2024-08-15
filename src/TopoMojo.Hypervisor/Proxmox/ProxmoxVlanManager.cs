using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor.Common;
using TopoMojo.Hypervisor.Proxmox.Models;

namespace TopoMojo.Hypervisor.Proxmox
{
    public interface IProxmoxVlanManager
    {
        Task<bool> HasNetwork(string networkName);
        Task<bool> HasNetworks(IEnumerable<string> networkNames);
        Task<IEnumerable<PveVnet>> Provision(IEnumerable<string> vnetNames, CancellationToken cancellationToken);
    }

    public class ProxmoxVlanManager : IProxmoxVlanManager
    {
        private readonly static Lazy<SemaphoreSlim> _deploySemaphore = new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(1));
        // defaults to a debounce period of 300ms, but can be changed using the `Pod__Vnet__ResetDebounceDuration`. A maximum
        // debounce can be set using `Pod__VNet__ResetDebounceMaxDuration`.
        private readonly static Lazy<DebouncePool<string>> _vnetDeployNames = new Lazy<DebouncePool<string>>(() => new DebouncePool<string>(300));
        private readonly static IMemoryCache _debounceBatchCreationCache = new MemoryCache(new MemoryCacheOptions { });

        private readonly HypervisorServiceConfiguration _hypervisorOptions;
        private readonly ILogger<ProxmoxVlanManager> _logger;
        private readonly IProxmoxNameService _nameService;
        private readonly ProxmoxClient _proxmox;

        public ProxmoxVlanManager
        (
            HypervisorServiceConfiguration hypervisorOptions,
            ILogger<ProxmoxClient> clientLogger,
            ILogger<ProxmoxVlanManager> logger,
            IProxmoxNameService nameService,
            Random random
        )
        {
            _hypervisorOptions = hypervisorOptions;
            _logger = logger;
            _nameService = nameService;

            _proxmox = new ProxmoxClient(
                hypervisorOptions,
                new ConcurrentDictionary<string, Vm>(),
                clientLogger,
                nameService,
                this,
                random);

            // update the debounce pool to use settings from config
            _vnetDeployNames.Value.DebouncePeriod = _hypervisorOptions.Vlan.ResetDebounceDuration;
            _vnetDeployNames.Value.MaxTotalDebounce = _hypervisorOptions.Vlan.ResetDebounceMaxDuration;
        }

        public async Task<IEnumerable<PveVnet>> Provision(IEnumerable<string> vnetNames, CancellationToken cancellationToken)
        {
            _logger.LogDebug($"Deploying vnets: {string.Join(",", vnetNames)}");
            var debouncedVnetNames = await _vnetDeployNames.Value.AddRange(vnetNames, CancellationToken.None);
            debouncedVnetNames.Items = debouncedVnetNames.Items.Distinct();

            try
            {
                await _deploySemaphore.Value.WaitAsync(cancellationToken);

                // check the cache to see if this debounce batch has already been created.
                // if so, just bail out and return what we already have
                _logger.LogDebug($"Looking up id {debouncedVnetNames.Id}");
                if (_debounceBatchCreationCache.TryGetValue<IEnumerable<PveVnet>>(debouncedVnetNames.Id, out var cachedDeployedVNets))
                {
                    var createdVnetNames = cachedDeployedVNets.Select(v => v.Alias).ToArray();

                    if (debouncedVnetNames.Items.All(newName => createdVnetNames.Contains(newName)))
                        return cachedDeployedVNets;
                }
                _logger.LogDebug($"Cache miss {debouncedVnetNames.Id}");

                // the proxmox client does all the heavy lifting of normalizing names, reloading the vnet host, etc.
                var deployedVnets = await _proxmox.CreateVnets(debouncedVnetNames.Items.Select(n => new CreatePveVnet { Alias = n, Zone = _hypervisorOptions.SDNZone }));

                if (deployedVnets.Any())
                {
                    // cache the id of the debounce batch we just handled (so later callers won't try to recreate the vnets)
                    _debounceBatchCreationCache
                        .GetOrCreate
                        (
                            debouncedVnetNames.Id,
                            entry =>
                            {
                                // cache this - we need this to remain at least long as the maximum possible debounce (if it's defined). If it is, 
                                // add a couple seconds for safety. if not, just double the min debounce.
                                var cacheDuration = _hypervisorOptions.Vlan.ResetDebounceMaxDuration != null ? _hypervisorOptions.Vlan.ResetDebounceMaxDuration.Value + 2000 : _hypervisorOptions.Vlan.ResetDebounceDuration;
                                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(cacheDuration);
                                return deployedVnets;
                            }
                        );

                    _logger.LogDebug($"Cached id {debouncedVnetNames.Id}");
                }

                return deployedVnets;
            }
            finally
            {
                _deploySemaphore.Value.Release();
            }
        }

        public Task<bool> HasNetwork(string networkName)
            => HasNetworks(new string[] { networkName });

        public async Task<bool> HasNetworks(IEnumerable<string> networkNames)
        {
            var hostNets = await _proxmox.GetVnets();

            return networkNames
                .Select(n => _nameService.ToPveName(n))
                .All(n => hostNets.Any(vnet => vnet.Alias == n));
        }
    }
}
