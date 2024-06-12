
using System;
using System.Collections.Generic;

namespace TopoMojo.Hypervisor.Proxmox
{
    public sealed class PveVmConfig
    {
        public string Boot { get; set; }
        public int Cores { get; set; }
        public string Cpu { get; set; }
        public string Digest { get; set; }
        public IEnumerable<PveNic> Nics { get; set; } = Array.Empty<PveNic>();
        public string OsType { get; set; }
        public long MemoryInBytes { get; set; }
        public string Smbios1 { get; set; }
        public int Sockets { get; set; }
    }
}
