using System;
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
        Task<IEnumerable<PveVnet>> DeleteVnets(IEnumerable<string> vnetNames);
        Task<IEnumerable<PveVnet>> DeleteVnetsByTerm(string term);
        Task<IEnumerable<PveVnet>> GetVnets();
        bool IsReserved(string networkName);
        Task<IEnumerable<PveVnet>> Provision(IEnumerable<string> vnetNames);
        string ResolvePveNetName(string topoName);
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

        private readonly int _cacheDurationMs;
        private readonly HypervisorServiceConfiguration _hypervisorOptions;
        private readonly ILogger<ProxmoxVlanManager> _logger;
        private readonly IProxmoxNameService _nameService;
        private readonly IProxmoxVnetsClient _vnetsApi;

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

        public async Task<IEnumerable<PveVnet>> DeleteVnets(IEnumerable<string> vnetNames)
        {
            _logger.LogDebug($"Deleting vnets: {string.Join(",", vnetNames)}");
            vnetNames = vnetNames
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct();

            if (!vnetNames.Any())
            {
                _logger.LogDebug($"No vnet names passed. Cancelling vnet delete.");
                return Array.Empty<PveVnet>();
            }

            // create the nets
            var results = await DebounceVnetOperations(vnetNames.Select(name => new PveVnetOperation(name, PveVnetOperationType.Create)));

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

                    results.AddRange(deployedVnets.Select(v => new PveVnetOperationResult
                    {
                        NetName = _nameService.FromPveName(v.Alias),
                        Vnet = v,
                        Type = PveVnetOperationType.Create
                    }));
                }

                if (vnetsToDelete.Any())
                {
                    var vnetNamesToDelete = vnetsToDelete.Select(n => _nameService.ToPveName(n.NetworkName));
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

                if (results.Any())
                {
                    // because we're allowing creates/deletes in the same debounce pool and trying to minimize reload calls,
                    // we manually reload proxmox's vnets at the end of the batch
                    await _vnetsApi.ReloadVnets();

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
