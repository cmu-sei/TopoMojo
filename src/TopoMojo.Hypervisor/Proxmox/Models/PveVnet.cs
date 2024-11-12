// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor.Proxmox.Models
{
    public class PveVnet
    {
        public int Tag { get; set; }
        public string Type { get; set; }
        public string Vnet { get; set; }
        public string Zone { get; set; }
        public string Alias { get; set; }
    }
}