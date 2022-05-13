// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VimClient;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.vSphere
{
    public abstract class NetworkManager : INetworkManager
    {
        public NetworkManager(
            VimReferences settings,
            ConcurrentDictionary<string, Vm> vmCache,
            VlanManager vlanManager
        ){
            _client = settings;
            _vmCache = vmCache;
            _vlanManager = vlanManager;
            _pgAllocation = new Dictionary<string, PortGroupAllocation>();
            _swAllocation = new Dictionary<string, int>();
        }

        protected VimReferences _client;
        protected readonly VlanManager _vlanManager;
        protected Dictionary<string, PortGroupAllocation> _pgAllocation;
        protected Dictionary<string, int> _swAllocation;
        protected ConcurrentDictionary<string, Vm> _vmCache;

        public async Task Initialize()
        {
            _pgAllocation = (await LoadPortGroups()).ToDictionary(p => p.Net);

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
                    if (!_swAllocation.ContainsKey(pg.Switch))
                        _swAllocation.Add(pg.Switch, 0);
                    _swAllocation[pg.Switch] += 1;
                }
            }

            //process vm counts
            var map = GetKeyMap();

            var vmnets = await GetVmNetworks(_client.pool);

            foreach (var vmnet in vmnets)
            {
                if (map.ContainsKey(vmnet.NetworkMOR))
                    map[vmnet.NetworkMOR].Counter += 1;
            }

            //remove empties
            await Clean();
        }

        public async Task Provision(VmTemplate template)
        {
            await Task.Delay(0);

            lock (_pgAllocation)
            {
                string sw = _client.UplinkSwitch;
                if (_client.dvs == null && _client.net != null && !template.UseUplinkSwitch)
                {
                    sw = template.IsolationTag.ToSwitchName();
                    if (!_swAllocation.ContainsKey(sw))
                    {
                        AddSwitch(sw).Wait();
                        _swAllocation.Add(sw, 0);
                    }
                }

                foreach (VmNet eth in template.Eth)
                {
                    if (!_pgAllocation.ContainsKey(eth.Net))
                    {
                        var pg = AddPortGroup(sw, eth).Result;
                        pg.Counter = 1;

                        _pgAllocation.Add(pg.Net, pg);

                        if (pg.VlanId > 0)
                        {
                            _vlanManager.Activate(new Vlan[] {
                                new Vlan {
                                    Id = pg.VlanId,
                                    Name = pg.Net,
                                    OnUplink = sw == _client.UplinkSwitch
                                }
                            });
                        }

                        if (_swAllocation.ContainsKey(sw))
                            _swAllocation[sw] += 1;

                    }
                    else
                    {
                        _pgAllocation[eth.Net].Counter += 1;
                    }

                    eth.Key = _pgAllocation[eth.Net].Key;
                }
            }
        }

        public async Task Unprovision(ManagedObjectReference vmMOR)
        {
            await Task.Delay(0);

            lock(_pgAllocation)
            {
                var map = GetKeyMap();

                var vmnets = GetVmNetworks(vmMOR).Result;

                foreach (var vmnet in vmnets)
                    if (map.ContainsKey(vmnet.NetworkMOR))
                        map[vmnet.NetworkMOR].Counter -= 1;
            }
        }

        public async Task Clean()
        {
            await Clean(null, true);
        }

        public async Task Clean(string tag)
        {
            await Clean(tag, false);
        }

        public async Task Clean(string tag, bool all)
        {
            await Task.Delay(0);
            lock(_pgAllocation)
            {
                //find empties with no associated vm's
                foreach (var pg in _pgAllocation.Values.Where(p => !all ? p.Net.Tag() == tag : true).ToArray())
                {
                    if (pg.Net.Tag().HasValue()
                        && pg.Counter < 1
                        && !_vmCache.Values.Any(v => v.Name.EndsWith(pg.Net.Tag()))
                    )
                    {
                        RemovePortgroup(pg.Key).Wait();

                        _pgAllocation.Remove(pg.Net);

                        _vlanManager.Deactivate(pg.Net);

                        if (_swAllocation.ContainsKey(pg.Switch))
                            _swAllocation[pg.Switch] -= 1;
                    }
                }

                foreach (var sw in _swAllocation.Keys.ToArray())
                {
                    if (_swAllocation[sw] < 1 && sw.Contains("#"))
                    {
                        RemoveSwitch(sw).Wait();

                        _swAllocation.Remove(sw);
                    }
                }
            }
        }

        private Dictionary<string, PortGroupAllocation> GetKeyMap()
        {
            //transform name dictionary to key dictionary
            var map = new Dictionary<string, PortGroupAllocation>();
            foreach (var pga in _pgAllocation.Values)
            {
                if (!map.ContainsKey(pga.Key))
                    map.Add(pga.Key, pga);
            }
            return map;
        }

        public abstract Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference managedObjectReference);
        public abstract Task<PortGroupAllocation[]> LoadPortGroups();
        public abstract Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth);
        public abstract Task RemovePortgroup(string pgReference);
        public abstract Task AddSwitch(string sw);
        public abstract Task RemoveSwitch(string sw);

        protected async Task<TaskInfo> WaitForVimTask(ManagedObjectReference task)
        {
            int i = 0;
            TaskInfo info = new TaskInfo();

            //iterate the search until complete or timeout occurs
            do
            {
                //check every so often
                await Task.Delay(2000);

                RetrievePropertiesResponse response = await _client.vim.RetrievePropertiesAsync(
                    _client.props,
                    FilterFactory.TaskFilter(task));

                ObjectContent[] oc = response.returnval;
                info = (TaskInfo)oc[0].propSet[0].val;
                i++;

            } while ((info.state == TaskInfoState.running || info.state == TaskInfoState.queued));

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
