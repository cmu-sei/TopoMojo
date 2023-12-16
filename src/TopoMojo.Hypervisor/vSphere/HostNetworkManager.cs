// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VimClient;
using TopoMojo.Hypervisor.Extensions;
using Microsoft.Extensions.Logging;

namespace TopoMojo.Hypervisor.vSphere
{
    public class HostNetworkManager : NetworkManager
    {
        public HostNetworkManager(
            ILogger logger,
            VimReferences settings,
            ConcurrentDictionary<string, Vm> vmCache,
            VlanManager vlanManager
        ) : base(logger, settings, vmCache, vlanManager)
        {

        }

        public override async Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth)
        {
            try
            {
                HostPortGroupSpec spec = new HostPortGroupSpec();
                spec.vswitchName = sw;
                spec.vlanId = eth.Vlan;
                spec.name = eth.Net;
                spec.policy = new HostNetworkPolicy();
                spec.policy.security = new HostNetworkSecurityPolicy();
                spec.policy.security.allowPromiscuous = true;
                spec.policy.security.allowPromiscuousSpecified = true;
                if (eth.Net.Untagged().EndsWith(_client.NetworkAllowAllSuffix))
                {
                    spec.policy.security.forgedTransmits = true;
                    spec.policy.security.forgedTransmitsSpecified = true;
                    spec.policy.security.macChanges = true;
                    spec.policy.security.macChangesSpecified = true;
                }

                await _client.vim.AddPortGroupAsync(_client.net, spec);

            } catch {}

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
            HostVirtualSwitchSpec swspec = new HostVirtualSwitchSpec();
            swspec.numPorts = 32;
            // swspec.policy = new HostNetworkPolicy();
            // swspec.policy.security = new HostNetworkSecurityPolicy();
            await _client.vim.AddVirtualSwitchAsync(_client.net, sw, swspec);
        }

        public override async Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference mor)
        {
            List<VmNetwork> result = new List<VmNetwork>();
            RetrievePropertiesResponse response = await _client.vim.RetrievePropertiesAsync(
                _client.props,
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
            return result.ToArray();
        }

        public override async Task<PortGroupAllocation[]> LoadPortGroups()
        {
            RetrievePropertiesResponse response = await _client.vim.RetrievePropertiesAsync(
                _client.props,
                FilterFactory.NetworkFilter(_client.net));

            ObjectContent[] oc = response.returnval;
            HostPortGroup[] pgs = (HostPortGroup[])oc[0].propSet[0].val;

            var list = new List<PortGroupAllocation>();

            foreach(HostPortGroup pg in pgs)
            {
                string net = pg.spec.name;

                if (!IsTenantNet(net))
                    continue;

                if (net.Contains("#"))
                    list.Add(new PortGroupAllocation
                    {
                        Net = net,
                        Key = net,
                        VlanId = pg.spec.vlanId,
                        Switch = pg.spec.vswitchName
                    });
            }
            return list.ToArray();
        }

        public override async Task<bool> RemovePortgroup(string pgReference)
        {
            try
            {
                await _client.vim.RemovePortGroupAsync(_client.net, pgReference);
            }
            catch {}
            return true;
        }

        public override async Task RemoveSwitch(string sw)
        {
            try
            {
                await _client.vim.RemoveVirtualSwitchAsync(_client.net, sw);
            }
            catch {}
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
