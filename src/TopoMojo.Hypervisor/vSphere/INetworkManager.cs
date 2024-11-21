// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Threading.Tasks;
using VimClient;

namespace TopoMojo.Hypervisor.vSphere
{
    public interface INetworkManager
    {
        //props: vim, pool, net, dvs, dvsuuid

        Task AddSwitch(string sw);
        Task RemoveSwitch(string sw);
        Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth);
        Task<PortGroupAllocation[]> AddPortGroups(string sw, VmNet[] eths);
        Task<PortGroupAllocation[]> RemovePortgroups(PortGroupAllocation[] pgs);
        Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference managedObjectReference);
        Task<PortGroupAllocation[]> LoadPortGroups();
        void UpdateEthernetCardBacking(VirtualEthernetCard card, string portgroupName);

        Task Initialize();
        Task Provision(VmTemplate template);
        Task ProvisionAll(VmNet[] template, bool useUplinkSwitch);
        Task Unprovision(ManagedObjectReference vmMOR);
        Task Clean(string tag = null);
        string Resolve(string net);
    }
}
