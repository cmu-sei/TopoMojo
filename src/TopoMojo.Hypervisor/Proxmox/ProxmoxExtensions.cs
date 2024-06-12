using System.Linq;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;

namespace TopoMojo.Hypervisor.Proxmox
{
    public static class ProxmoxExtensions
    {
        public static long GetId(this Vm vm)
        {
            return long.Parse(vm.Id);
        }
    }
}