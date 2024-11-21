// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VimClient;
using TopoMojo.Hypervisor.Extensions;
using Microsoft.Extensions.Logging;

namespace TopoMojo.Hypervisor.vSphere
{
    public class HostNetworkManager(
        ILogger logger,
        VimReferences settings,
        ConcurrentDictionary<string, Vm> vmCache,
        VlanManager vlanManager
        ) : NetworkManager(logger, settings, vmCache, vlanManager)
    {
        public override async Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth)
        {
            try
            {
                HostPortGroupSpec spec = new()
                {
                    vswitchName = sw,
                    vlanId = eth.Vlan,
                    name = eth.Net,
                    policy = new()
                };
                spec.policy.security = new()
                {
                    allowPromiscuous = true,
                    allowPromiscuousSpecified = true
                };
                if (eth.Net.Untagged().EndsWith(_client.NetworkAllowAllSuffix))
                {
                    spec.policy.security.forgedTransmits = true;
                    spec.policy.security.forgedTransmitsSpecified = true;
                    spec.policy.security.macChanges = true;
                    spec.policy.security.macChangesSpecified = true;
                }

                await _client.Vim.AddPortGroupAsync(_client.Net, spec);

            }
            catch { }

            return new PortGroupAllocation
            {
                Net = eth.Net,
                Key = eth.Net,
                VlanId = eth.Vlan,
                Switch = sw
            };
        }

        public override async Task AddSwitch(string sw)
        {
            HostVirtualSwitchSpec swspec = new()
            {
                numPorts = 32
            };
            // swspec.policy = new HostNetworkPolicy();
            // swspec.policy.security = new HostNetworkSecurityPolicy();
            await _client.Vim.AddVirtualSwitchAsync(_client.Net, sw, swspec);
        }

        public override async Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference mor)
        {
            List<VmNetwork> result = [];
            RetrievePropertiesResponse response = await _client.Vim.RetrievePropertiesAsync(
                _client.Props,
                FilterFactory.VmFilter(mor, "name config.hardware"));
            ObjectContent[] oc = response.returnval;
            foreach (ObjectContent obj in oc)
            {
                string vmName = obj.GetProperty("name").ToString();

                if (!IsTenantVm(vmName))
                    continue;

                VirtualHardware hardware = obj.GetProperty("config.hardware") as VirtualHardware;
                foreach (VirtualEthernetCard card in hardware.device.OfType<VirtualEthernetCard>())
                {
                    result.Add(new VmNetwork
                    {
                        NetworkMOR = ((VirtualEthernetCardNetworkBackingInfo)card.backing).deviceName,
                        VmName = vmName
                    });
                }
            }
            return [.. result];
        }

        public override async Task<PortGroupAllocation[]> LoadPortGroups()
        {
            RetrievePropertiesResponse response = await _client.Vim.RetrievePropertiesAsync(
                _client.Props,
                FilterFactory.NetworkFilter(_client.Net));

            ObjectContent[] oc = response.returnval;
            HostPortGroup[] pgs = (HostPortGroup[])oc[0].propSet[0].val;

            var list = new List<PortGroupAllocation>();

            foreach (HostPortGroup pg in pgs)
            {
                string net = pg.spec.name;

                if (!IsTenantNet(net))
                    continue;

                if (net.Contains('#'))
                    list.Add(new PortGroupAllocation
                    {
                        Net = net,
                        Key = net,
                        VlanId = pg.spec.vlanId,
                        Switch = pg.spec.vswitchName
                    });
            }
            return [.. list];
        }

        public override async Task<PortGroupAllocation[]> RemovePortgroups(PortGroupAllocation[] pgs)
        {
            foreach (var pg in pgs.ToArray())
            {
                try
                {
                    await _client.Vim.RemovePortGroupAsync(_client.Net, pg.Key);
                }
                catch { }
            }
            return pgs;
        }

        public override async Task RemoveSwitch(string sw)
        {
            try
            {
                await _client.Vim.RemoveVirtualSwitchAsync(_client.Net, sw);
            }
            catch { }
        }

        public override void UpdateEthernetCardBacking(VirtualEthernetCard card, string portgroupName)
        {
            if (card != null)
            {
                if (card.backing is VirtualEthernetCardNetworkBackingInfo info)
                    info.deviceName = portgroupName;

                card.connectable = new VirtualDeviceConnectInfo()
                {
                    connected = true,
                    startConnected = true,
                };
            }
        }
    }
}
