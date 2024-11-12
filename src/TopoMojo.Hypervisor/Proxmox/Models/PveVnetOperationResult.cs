// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor.Proxmox.Models
{
    public sealed class PveVnetOperationResult
    {
        public string NetName { get; set; }
        public PveVnet Vnet { get; set; }
        public PveVnetOperationType Type { get; set; } = PveVnetOperationType.Create;

        public override string ToString()
        {
            return $"{NetName} :: {Vnet.Zone}.{Vnet.Alias} :: {Type}";
        }
    }
}
