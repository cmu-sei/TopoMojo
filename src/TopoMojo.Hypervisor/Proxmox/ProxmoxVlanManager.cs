using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor.Common;
using TopoMojo.Hypervisor.Extensions;
using TopoMojo.Hypervisor.Proxmox.Models;

namespace TopoMojo.Hypervisor.Proxmox
{
    public interface IProxmoxVlanManager
    {
        Task<IEnumerable<PveVnet>> DeleteVnets(IEnumerable<string> vnetNames, bool force);
        Task<IEnumerable<PveVnet>> DeleteVnetsByTerm(string term);
        Task<IEnumerable<PveVnet>> GetVnets();
        bool IsReserved(string networkName);
        Task<IEnumerable<PveVnet>> Provision(IEnumerable<string> vnetNames);
        string ResolvePveNetName(string topoName);
        Task Clean(ConcurrentDictionary<string, Vm> vmCache, string tag = null);
        Task Initialize();
    }

    public class ProxmoxVlanManager : IProxmoxVlanManager
    {
        // ABOUT THE STATIC MEMBERS
        // this service is currently placed into the DI container as a singleton, but I wanted to support the case that it ever becomes
        // scoped. the static members are important to how the class works across all instances, where the individual ones are either
        // injected or trivial
        private readonly static Lazy<SemaphoreSlim> _deploySemaphore = new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(1));
        // defaults to a debounce period of 300ms, but can be changed using the `Pod__Vnet__ResetDebounceDuration`. A maximum
        // debounce can be set using `Pod__VNet__ResetDebounceMaxDuration`.
        private readonly static Lazy<DebouncePool<PveVnetOperation>> _vnetOpsPool = new Lazy<DebouncePool<PveVnetOperation>>(() => new DebouncePool<PveVnetOperation>());
        private readonly static IMemoryCache _recentVnetOpsCache = new MemoryCache(new MemoryCacheOptions { });
        private readonly static IDictionary<string, int> _reservedVnetIds = new Dictionary<string, int>();
        private readonly static IMemoryCache _recentVnetCache = new MemoryCache(new MemoryCacheOptions { });

        private readonly int _cacheDurationMs;
        private readonly int _recentExpirationMinutes = 5;
        private readonly int _lastReloadMaxMinutes = 30;
        private readonly HypervisorServiceConfiguration _hypervisorOptions;
        private readonly ILogger<ProxmoxVlanManager> _logger;
        private readonly IProxmoxNameService _nameService;
        private readonly IProxmoxVnetsClient _vnetsApi;

        private DateTimeOffset _lastReload = DateTimeOffset.UtcNow;

        public ProxmoxVlanManager
        (
            HypervisorServiceConfiguration hypervisorOptions,
            ILogger<ProxmoxVlanManager> logger,
            IProxmoxNameService nameService,
            IProxmoxVnetsClient vnetsApi
        )
        {
            _hypervisorOptions = hypervisorOptions;
            _logger = logger;
            _nameService = nameService;
            _vnetsApi = vnetsApi;

            // update the debounce pool to use settings from config
            _vnetOpsPool.Value.DebouncePeriod = _hypervisorOptions.Vlan.ResetDebounceDuration;
            _vnetOpsPool.Value.MaxTotalDebounce = _hypervisorOptions.Vlan.ResetDebounceMaxDuration;

            // cache this - we need this to remain at least long as the maximum possible debounce (if it's defined). If it is, 
            // add a couple seconds for safety. if not, just double the min debounce up to a minimum of two seconds
            _cacheDurationMs = _hypervisorOptions.Vlan.ResetDebounceMaxDuration != null ?
                _hypervisorOptions.Vlan.ResetDebounceMaxDuration.Value + 2000 :
                Math.Max(_hypervisorOptions.Vlan.ResetDebounceDuration * 2, 2000);

            // reserve the vlans specified as "global" in the application's config
            Reserve(hypervisorOptions.Vlan.Reservations);
        }

        /// <summary>
        /// Delete the specified vnets
        /// </summary>
        /// <param name="vnetNames"></param>
        /// <param name="force">Force a reload check by continuing even with an empty list</param>
        /// <returns></returns>
        public async Task<IEnumerable<PveVnet>> DeleteVnets(IEnumerable<string> vnetNames, bool force = false)
        {
            _logger.LogDebug($"Requested to delete vnets: {string.Join(",", vnetNames)}");
            vnetNames = vnetNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct();

            if (!force && !vnetNames.Any())
            {
                _logger.LogDebug($"No vnet names passed. Cancelling vnet delete.");
                return Array.Empty<PveVnet>();
            }

            // create the nets
            var results = await DebounceVnetOperations(vnetNames.Select(name => new PveVnetOperation(name, PveVnetOperationType.Delete)));

            // the results contain all network operations performed this debounce, but we only want to send back the ones related to
            // the requested names
            var pveVnetNames = vnetNames.Select(name => _nameService.ToPveName(name)).ToArray();
            return results
                .Where(r => pveVnetNames.Contains(r.Vnet.Alias))
                .Select(r => r.Vnet);
        }

        public async Task<IEnumerable<PveVnet>> DeleteVnetsByTerm(string term)
        {
            var vnets = await _vnetsApi.GetVnets();
            var matchingVnetDeleteOps = vnets
                .Where(v => v.Alias.Contains(term))
                .Select(v => new PveVnetOperation
                (
                    v.Alias,
                    PveVnetOperationType.Delete
                ));

            var results = await this.DebounceVnetOperations(matchingVnetDeleteOps);
            return results
                .Where(r => r.NetName.Contains(term))
                .Select(r => r.Vnet);
        }

        public Task<IEnumerable<PveVnet>> GetVnets()
            => _vnetsApi.GetVnets();

        public bool IsReserved(string networkName)
            => _reservedVnetIds.ContainsKey(networkName);

        public async Task<IEnumerable<PveVnet>> Provision(IEnumerable<string> vnetNames)
        {
            _logger.LogDebug($"Deploying vnets: {string.Join(",", vnetNames)}");
            var requestedVnetNames = vnetNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct();

            if (!requestedVnetNames.Any())
            {
                _logger.LogDebug($"No vnet names passed. Cancelling vnet deploy.");
                return Array.Empty<PveVnet>();
            }

            // create the nets
            var results = await DebounceVnetOperations(requestedVnetNames.Select(name => new PveVnetOperation(name, PveVnetOperationType.Create)));

            // the results contain all network operations performed this debounce, but we only want to send back the ones related to
            // the requested names
            var pveVnetNames = requestedVnetNames.Select(name => _nameService.ToPveName(name)).ToArray();
            return results
                .Where(r => pveVnetNames.Contains(r.Vnet.Alias))
                .Select(r => r.Vnet);
        }

        public string ResolvePveNetName(string topoName)
            => IsReserved(topoName) ? topoName : _nameService.ToPveName(topoName);

        public async Task Initialize()
        {
            _logger.LogDebug($"initializing nets");

            var vnets = await _vnetsApi.GetVnets();

            foreach (var vnet in vnets)
            {
                _logger.LogDebug($"Adding to recent vnet cache: {vnet.Alias}");
                AddToRecentCache(vnet);
            }
        }

        public async Task Clean(ConcurrentDictionary<string, Vm> vmCache, string tag = null)
        {
            _logger.LogDebug($"cleaning nets [{tag}]");

            var vnets = await _vnetsApi.GetVnets();
            var vnetsToDelete = new List<string>();

            if (!string.IsNullOrEmpty(tag))
            {
                vnets = vnets.Where(x => _nameService.FromPveName(x.Alias).Tag() == tag);
            }

            // exclude non-tagged
            vnets = vnets.Where(x => _nameService.FromPveName(x.Alias).Contains('#'));

            // find portgroups with no associated vm's
            foreach (var vnet in vnets)
            {
                string id = _nameService.FromPveName(vnet.Alias).Tag();

                // if vm's still exist, skip
                if (!vmCache.Values.Any(v => _nameService.FromPveName(v.Name).Tag() == id))
                {
                    vnetsToDelete.Add(vnet.Alias);
                }
            }

            await DeleteVnets(vnetsToDelete, force: true);
        }

        private void AddToRecentCache(PveVnet vnet)
        {
            _recentVnetCache.Set(
                vnet.Alias,
                vnet.Vnet,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_recentExpirationMinutes)
                });
        }

        private void Reserve(IEnumerable<Vlan> vlans)
        {
            var vlanNames = vlans.Select(v => v.Name).Distinct().ToArray();

            if (vlanNames.Length != vlans.Count())
                throw new InvalidOperationException($"Can't reserve virtual networks with duplicate names: {string.Join(",", vlans.Select(v => v.Name))}");

            foreach (var vlan in vlans)
            {
                _reservedVnetIds.Add(vlan.Name, vlan.Id);
            }
        }

        private async Task<IEnumerable<PveVnetOperationResult>> DebounceVnetOperations(IEnumerable<PveVnetOperation> requestedOperations)
        {
            var debouncedOperations = await _vnetOpsPool.Value.AddRange(requestedOperations, CancellationToken.None);

            // PveVnetOperation implements object comparison, so we can use .Distinct to ensure we don't ever
            // try a duplicate op (at least not in the same debounce)
            debouncedOperations.Items = debouncedOperations.Items.Distinct();

            try
            {
                await _deploySemaphore.Value.WaitAsync(CancellationToken.None);

                // check the cache to see if this debounce batch has already been created.
                // if so, just bail out and return what we already have
                _logger.LogDebug($"Looking up id {debouncedOperations.Id}");
                if (_recentVnetOpsCache.TryGetValue<IEnumerable<PveVnetOperationResult>>(debouncedOperations.Id, out var cachedOperations))
                {
                    return cachedOperations.Where(o => requestedOperations.Any(req => req.Equals(o)));
                }
                _logger.LogDebug($"Cache miss {debouncedOperations.Id}");

                var results = new List<PveVnetOperationResult>();
                var vnetsToCreate = debouncedOperations.Items.Where(op => op.Type == PveVnetOperationType.Create).ToArray();
                var vnetsToDelete = debouncedOperations.Items.Where(op => op.Type == PveVnetOperationType.Delete).ToArray();

                if (vnetsToCreate.Any())
                {
                    var vnetNamesToCreate = vnetsToCreate.Select(v => _nameService.ToPveName(v.NetworkName));
                    var deployedVnets = await _vnetsApi.CreateVnets(vnetsToCreate.Select(n => new CreatePveVnet
                    {
                        Alias = _nameService.ToPveName(n.NetworkName),
                        Zone = _hypervisorOptions.SDNZone,
                        Tag = _reservedVnetIds.TryGetValue(n.NetworkName, out var reservedId) ? reservedId : default(int?)
                    }));

                    // Add created vnets to recent cache
                    foreach (var vnet in deployedVnets)
                    {
                        AddToRecentCache(vnet);
                    }

                    results.AddRange(deployedVnets.Select(v => new PveVnetOperationResult
                    {
                        NetName = _nameService.FromPveName(v.Alias),
                        Vnet = v,
                        Type = PveVnetOperationType.Create
                    }));
                }

                if (vnetsToDelete.Any())
                {
                    // Only delete vnets that haven't been created recently to avoid accidentally deleting
                    // a vnet that is in use. Also, don't delete a vnet if we created it in this batch
                    var vnetNamesToDelete = vnetsToDelete
                        .Select(n => _nameService.ToPveName(n.NetworkName))
                        .Where(x =>
                            !vnetsToCreate.Any(y => y.NetworkName == x) &&
                            !_recentVnetCache.TryGetValue(x, out _));

                    var deletedVnets = await _vnetsApi.DeleteVnets(vnetNamesToDelete);

                    foreach (var deletedVnet in deletedVnets)
                    {
                        results.Add
                        (
                            new PveVnetOperationResult
                            {
                                NetName = _nameService.FromPveName(deletedVnet.Alias),
                                Vnet = deletedVnet,
                                Type = PveVnetOperationType.Delete
                            });
                    }
                }

                _logger.LogDebug($"Batch {debouncedOperations.Id} results: {string.Join(",", results)}");

                if (results.Any(x =>
                    x.Type == PveVnetOperationType.Create) ||
                    _lastReload.AddMinutes(_lastReloadMaxMinutes) < DateTimeOffset.UtcNow)
                {
                    // because we're allowing creates/deletes in the same debounce pool and trying to minimize reload calls,
                    // we manually reload proxmox's vnets at the end of the batch
                    // skip reload if only delete operations - we'll catch them on the next reload
                    await _vnetsApi.ReloadVnets();
                    _lastReload = DateTimeOffset.UtcNow;

                    // cache the id of the debounce batch we just handled (so later callers won't try to recreate/redelete the vnets)
                    _recentVnetOpsCache
                        .GetOrCreate<IEnumerable<PveVnetOperationResult>>
                        (
                            debouncedOperations.Id,
                            entry =>
                            {
                                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMilliseconds(_cacheDurationMs);
                                return results;
                            }
                        );

                    _logger.LogDebug($"Cached id {debouncedOperations.Id}");
                }

                return results;
            }
            finally
            {
                _deploySemaphore.Value.Release();
            }
        }
    }
}
