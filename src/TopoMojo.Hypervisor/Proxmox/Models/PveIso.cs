// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor.Proxmox.Models
{
    public class PveIso
    {
        public string Volid { get; set; }
        public string Format { get; set; }
        public string Content { get; set; }
        public int Ctime { get; set; }
        public long Size { get; set; }

        public string Name
        {
            get
            {
                // Volid format: "storage:iso/filename.iso" or "storage:/iso/filename.iso"
                // Extract just the filename (last part after final /)
                var parts = this.Volid.Split('/');
                return parts[parts.Length - 1];
            }
        }

        public string ScopeId => Decoded.scopeId;

        public string ScopedFileName => Decoded.fileName ?? Name;

        public string DisplayName => ScopeId is null ? Name : $"{ScopeId}/{ScopedFileName}";

        private (string scopeId, string fileName) Decoded
        {
            get
            {
                ProxmoxIsoNaming.TryDecode(Name, out var scopeId, out var fileName);
                return (scopeId, fileName);
            }
        }
    }
}
