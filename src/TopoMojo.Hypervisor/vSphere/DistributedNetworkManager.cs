// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VimClient;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.vSphere
{
    public class DistributedNetworkManager : NetworkManager
    {
        public DistributedNetworkManager(
            VimReferences settings,
            ConcurrentDictionary<string, Vm> vmCache,
            VlanManager vlanManager
        ) : base(settings, vmCache, vlanManager)
        {

        }

        public override async Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth)
        {
            var mor = new ManagedObjectReference();
            try
            {
                bool allowAll = eth.Net.Untagged().EndsWith(_client.NetworkAllowAllSuffix);

                var spec = new DVPortgroupConfigSpec()
                {
                    name = eth.Net,
                    autoExpand = true,
                    type = "earlyBinding",

                    defaultPortConfig = new VMwareDVSPortSetting
                    {
                        vlan = new VmwareDistributedVirtualSwitchVlanIdSpec
                        {
                            vlanId = eth.Vlan
                        },
                        securityPolicy = new DVSSecurityPolicy()
                        {
                            allowPromiscuous = new BoolPolicy()
                            {
                                value = true,
                                valueSpecified = true
                            },
                            forgedTransmits = new BoolPolicy()
                            {
                                value = allowAll,
                                valueSpecified = allowAll
                            },
                            macChanges = new BoolPolicy()
                            {
                                value = allowAll,
                                valueSpecified = allowAll
                            }

                        }
                    }
                };

                var task = await _client.vim.CreateDVPortgroup_TaskAsync(_client.dvs, spec);
                var info = await WaitForVimTask(task);
                mor = info.result as ManagedObjectReference;
            }
            catch { }
            return new PortGroupAllocation
            {
                Net = eth.Net,
                Key = mor.AsString(),
                VlanId = eth.Vlan,
                Switch = _client.UplinkSwitch
            };
        }

        public override Task AddSwitch(string sw)
        {
            return Task.FromResult(0);
        }

        public override async Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference mor)
        {
            var result = new List<VmNetwork>();
            RetrievePropertiesResponse response = await _client.vim.RetrievePropertiesAsync(
                _client.props,
                FilterFactory.VmFilter(mor, "name config"));
            ObjectContent[] oc = response.returnval;

            foreach (ObjectContent obj in oc)
            {
                string vmName = obj.GetProperty("name").ToString();

                VirtualMachineConfigInfo config = obj.GetProperty("config") as VirtualMachineConfigInfo;

                foreach (VirtualEthernetCard card in config.hardware.device.OfType<VirtualEthernetCard>())
                {
                    if (card.backing is VirtualEthernetCardDistributedVirtualPortBackingInfo)
                    {
                        var back = card.backing as VirtualEthernetCardDistributedVirtualPortBackingInfo;

                        result.Add(new VmNetwork
                        {
                            NetworkMOR = $"DistributedVirtualPortgroup|{back.port.portgroupKey}",
                            VmName = vmName
                        });
                    }
                }

            }

            return result.ToArray();
        }

        public override async Task<PortGroupAllocation[]> LoadPortGroups()
        {
            var list = new List<PortGroupAllocation>();

            RetrievePropertiesResponse response = await _client.vim.RetrievePropertiesAsync(
                _client.props,
                FilterFactory.DistributedPortgroupFilter(_client.cluster));

            ObjectContent[] clunkyTree = response.returnval;
            foreach (var dvpg in clunkyTree.FindType("DistributedVirtualPortgroup"))
            {
                var config = (DVPortgroupConfigInfo)dvpg.GetProperty("config");
                if (config.distributedVirtualSwitch.Value == _client.dvs.Value)
                {
                    string net = dvpg.GetProperty("name") as string;

                    if (Regex.Match(net, _client.ExcludeNetworkMask).Success)
                        continue;

                    if (
                        config.defaultPortConfig is VMwareDVSPortSetting
                        && ((VMwareDVSPortSetting)config.defaultPortConfig).vlan is VmwareDistributedVirtualSwitchVlanIdSpec
                    )
                    {
                        list.Add(
                            new PortGroupAllocation
                            {
                                Net = net,
                                Key = dvpg.obj.AsString(),
                                VlanId = ((VmwareDistributedVirtualSwitchVlanIdSpec)((VMwareDVSPortSetting)config.defaultPortConfig).vlan).vlanId,
                                Switch = _client.UplinkSwitch
                            }
                        );
                    }
                }
            }

            return list.ToArray();
        }

        public override async Task RemovePortgroup(string pgReference)
        {
            try
            {
                await _client.vim.Destroy_TaskAsync(pgReference.AsReference());
            }
            catch { }
        }

        public override Task RemoveSwitch(string sw)
        {
            return Task.FromResult(0);
        }

        public override void UpdateEthernetCardBacking(VirtualEthernetCard card, string portgroupName)
        {
            if (card != null)
            {
                if (card.backing is VirtualEthernetCardDistributedVirtualPortBackingInfo)
                {
                    string netMorName = this.Resolve(portgroupName);

                    card.backing = new VirtualEthernetCardDistributedVirtualPortBackingInfo
                    {
                        port = new DistributedVirtualSwitchPortConnection
                        {
                            switchUuid = _client.DvsUuid,
                            portgroupKey = netMorName.AsReference().Value
                        }
                    };
                }

                card.connectable = new VirtualDeviceConnectInfo()
                {
                    connected = true,
                    startConnected = true,
                };
            }
        }
    }
}
