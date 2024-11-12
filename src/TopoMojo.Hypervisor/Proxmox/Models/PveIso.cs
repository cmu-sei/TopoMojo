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
                return this.Volid.Split('/')[1];
            }
        }

        public string DisplayName
        {
            get
            {
                return this.Name.Replace('#', '/');
            }
        }
    }
}
