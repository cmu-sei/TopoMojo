// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Hypervisor.Proxmox;

namespace TopoMojo.Hypervisor.Proxmox.Models
{
    public sealed record ProxmoxIsoFile(string Volid, string Name, string ScopeId, string ScopedFileName)
    {
        public string DisplayName => ScopeId is null ? Name : $"{ScopeId}/{ScopedFileName}";

        public static ProxmoxIsoFile From(PveIso volume, string separator)
        {
            var name = volume.Name;
            return ProxmoxIsoNaming.TryDecode(name, separator, out var scopeId, out var fileName)
                ? new ProxmoxIsoFile(volume.Volid, name, scopeId, fileName)
                : new ProxmoxIsoFile(volume.Volid, name, null, name);
        }
    }
}
