// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Threading.Tasks;
using NetVimClient;

namespace TopoMojo.Hypervisor.vSphere
{
    public interface INetworkManager
    {
        //props: vim, pool, net, dvs, dvsuuid

        Task AddSwitch(string sw);
        Task RemoveSwitch(string sw);
        Task<PortGroupAllocation> AddPortGroup(string sw, VmNet eth);
        Task RemovePortgroup(string pgReference);
        Task<VmNetwork[]> GetVmNetworks(ManagedObjectReference managedObjectReference);
        Task<PortGroupAllocation[]> LoadPortGroups();
        void UpdateEthernetCardBacking(VirtualEthernetCard card, string portgroupName);

        Task Initialize();
        Task Provision(VmTemplate template);
        Task Unprovision(ManagedObjectReference vmMOR);
        Task Clean(string tag);
        Task Clean();
        string Resolve(string net);
    }
}
