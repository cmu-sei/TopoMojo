// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VimClient;
using TopoMojo.Hypervisor.Extensions;
using Microsoft.Extensions.Logging;
using System;

namespace TopoMojo.Hypervisor.vSphere
{
    public abstract class NetworkManager(
        ILogger logger,
        VimReferences settings,
        ConcurrentDictionary<string, Vm> vmCache,
        VlanManager vlanManager
        ) : INetworkManager
    {
        protected VimReferences _client = settings;
        protected readonly VlanManager _vlanManager = vlanManager;
        protected Dictionary<string, PortGroupAllocation> _pgAllocation = [];
        protected Dictionary<string, int> _swAllocation = [];
        protected ConcurrentDictionary<string, Vm> _vmCache = vmCache;
        protected ILogger _logger = logger;

        readonly int _clean_network_buffer_minutes = -2;

        public async Task Initialize()
        {
            DateTimeOffset ts = DateTimeOffset.UtcNow.AddMinutes(_clean_network_buffer_minutes - 1);
            var existing = await LoadPortGroups();
            foreach (var pg in existing)
                pg.Timestamp = ts;

            _pgAllocation = existing.DistinctBy(p => p.Net).ToDictionary(p => p.Net);

            _vlanManager.Activate(
                _pgAllocation.Values.Select(p => new Vlan
                {
                    Id = p.VlanId,
                    Name = p.Net,
                    OnUplink = p.Switch == _client.UplinkSwitch
                })
                .ToArray()
            );

            //process switch counts
            foreach (var pg in _pgAllocation.Values)
            {
                if (pg.Switch.HasValue())
                {
                    _swAllocation.TryAdd(pg.Switch, 0);
                    _swAllocation[pg.Switch] += 1;
                }
            }

            //process vm counts
            var map = GetKeyMap();

            var vmnets = await GetVmNetworks(_client.Pool);

            foreach (var vmnet in vmnets)
            {
                if (map.TryGetValue(vmnet.NetworkMOR, out PortGroupAllocation value))
                    value.Counter += 1;
            }

        }

        public async Task Provision(VmTemplate template)
        {
            await Task.Delay(0);

            await ProvisionAll(template.Eth, template.UseUplinkSwitch);

            lock (_pgAllocation)
            {
                foreach (var eth in template.Eth)
                {
                    if (_pgAllocation.TryGetValue(eth.Net, out PortGroupAllocation value)) {
                        eth.Key = value.Key;
                        value.Counter += 1;
                    } else {
                        throw new Exception($"Network not provisioned: {eth.Net}");
                    }

                }
            }
        }

        public async Task ProvisionAll(VmNet[] nets, bool useUplinkSwitch)
        {
            await Task.Delay(0);

            lock (_pgAllocation)
            {
                string sw = _client.UplinkSwitch;
                if (_client.Dvs == null && _client.Net != null && !useUplinkSwitch)
                {
                    sw = nets[0].Net.Tag().ToSwitchName();
                    if (_swAllocation.TryAdd(sw, 0))
                    {
                        AddSwitch(sw).Wait();
                    }
                }

                // filter only unallocated nets
                var manifest = nets
                    .DistinctBy(e => e.Net)
                    .Where(e => _pgAllocation.ContainsKey(e.Net).Equals(false))
                    .ToArray()
                ;

                _logger.LogDebug("AddPortGroups for {count} nets...", manifest.Length);
                var pgs = AddPortGroups(sw, manifest).Result;

                _vlanManager.Activate(
                    pgs.Select(p => new Vlan
                    {
                        Id = p.VlanId,
                        Name = p.Net,
                        OnUplink = sw == _client.UplinkSwitch
                    }).ToArray()
                );

                foreach (var pg in pgs)
                {
                    pg.Timestamp = DateTimeOffset.UtcNow;
                    _pgAllocation.Add(pg.Net, pg);
                }

                if (_swAllocation.ContainsKey(sw))
                    _swAllocation[sw] += pgs.Length;

                var missing = manifest.ExceptBy(pgs.Select(p => p.Net), m => m.Net);
                if (missing.Any()) {
                    throw new Exception($"Failed to provision nets:\n\t{string.Join("\n\t", missing.Select(m => m.Net))}");
                }
            }
        }

        public async Task Unprovision(ManagedObjectReference vmMOR)
        {
            await Task.Delay(0);

            lock (_pgAllocation)
            {
                var map = GetKeyMap();

                var vmnets = GetVmNetworks(vmMOR).Result;

                foreach (var vmnet in vmnets)
                    if (map.TryGetValue(vmnet.NetworkMOR, out PortGroupAllocation value))
                    {
                        value.Timestamp = DateTimeOffset.UtcNow;
                        value.Counter -= 1;
                    }
            }
        }

        public async Task Clean(string tag = null)
        {
            await Task.Delay(0);
            _logger.LogDebug("cleaning nets for id [{tag}]", tag ?? "stale");

            lock (_pgAllocation)
            {
                IEnumerable<PortGroupAllocation> q = string.IsNullOrEmpty(tag)
                    ? _pgAllocation.Values
                    : _pgAllocation.Values.Where(p => p.Net.EndsWith(tag))
                ;

                // exclude non-tagged and recently-touched portgroups
                DateTimeOffset mark = DateTimeOffset.UtcNow.AddMinutes(_clean_network_buffer_minutes);
                q = q.Where(p => p.Net.Contains('#') && p.Timestamp < mark)
                    .OrderBy(p => p.Timestamp)
                ;

                // find portgroups with no associated vm's
                var vm_space = _vmCache.Values.Select(v => v.Name.Tag()).Distinct();
                var pg_space = q.Select(p => p.Net.Tag()).Distinct();
                var inactive = pg_space.Except(vm_space).ToArray();
                var manifest = q.Where(p => inactive.Contains(p.Net.Tag())).ToArray();

                var removed_pgs = RemovePortgroups(manifest).Result;
                foreach (var pg in removed_pgs)
                {
                    _pgAllocation.Remove(pg.Net);
                    _vlanManager.Deactivate(pg.Net);

                    if (_swAllocation.ContainsKey(pg.Switch))
                        _swAllocation[pg.Switch] -= 1;
                }

                foreach (var sw in _swAllocation.Keys.ToArray())
                {
                    if (_swAllocation[sw] < 1 && sw.Contains('#'))
                    {
                        RemoveSwitch(sw).Wait();
                        _swAllocation.Remove(sw);
                    }
                }
            }
        }

        public bool IsTenantVm(string name)
        {
            return name.ToTenant() == _client.TenantId;
        }

        public bool IsTenantNet(string net)
        {
            return net.Contains('#')
                ? net.ToTenant() == _client.TenantId
                : _vlanManager.Contains(net)
            ;
        }

        private Dictionary<string, PortGroupAllocation> GetKeyMap()
        {
            //transform name dictionary to key dictionary
            var map = new Dictionary<string, PortGroupAllocation>();
            foreach (var pga in _pgAllocation.Values)
            {
                map.TryAdd(pga.Key, pga);
            }
            return map;
        }

        public abstract Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference managedObjectReference);
        public abstract Task<PortGroupAllocation[]> LoadPortGroups();
        public abstract Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth);
        public virtual async Task<PortGroupAllocation[]> AddPortGroups(string sw, VmNet[] eths)
        {
            List<PortGroupAllocation> pgs = [];
            foreach (var eth in eths)
                pgs.Add(
                    await AddPortGroup(sw, eth)
                );
            return [.. pgs];
        }
        public abstract Task<PortGroupAllocation[]> RemovePortgroups(PortGroupAllocation[] gps);
        public abstract Task AddSwitch(string sw);
        public abstract Task RemoveSwitch(string sw);

        protected async Task<TaskInfo> WaitForVimTask(ManagedObjectReference task)
        {
            int i = 0;
            TaskInfo info;

            //iterate the search until complete or timeout occurs
            do
            {
                //check every so often
                await Task.Delay(2000);

                RetrievePropertiesResponse response = await _client.Vim.RetrievePropertiesAsync(
                    _client.Props,
                    FilterFactory.TaskFilter(task));

                ObjectContent[] oc = response.returnval;
                info = (TaskInfo)oc[0].propSet[0].val;
                i++;

            } while (info.state == TaskInfoState.running || info.state == TaskInfoState.queued);

            //return the task info
            return info;
        }

        public string Resolve(string net)
        {
            return _pgAllocation[net]?.Key ?? "notfound";
        }

        public abstract void UpdateEthernetCardBacking(VirtualEthernetCard card, string portgroupName);
    }
}
