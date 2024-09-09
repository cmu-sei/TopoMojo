using System.Collections.Generic;

namespace TopoMojo.Hypervisor.Proxmox
{
    public sealed class PveVmUpdateConfig
    {
        public IDictionary<int, string> NetAssignments { get; private set; } = new Dictionary<int, string>();
    }
}
