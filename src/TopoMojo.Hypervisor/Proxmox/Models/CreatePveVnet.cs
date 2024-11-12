// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor.Proxmox.Models
{
    public sealed class CreatePveVnet
    {
        public string Alias { get; set; }
        public string Zone { get; set; }

        /// <summary>
        /// An integer tag will be automatically generated during creation if this isn't set
        /// </summary>
        public int? Tag { get; set; } = null;
    }
}
