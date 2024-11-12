// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor.Proxmox
{
    public sealed class PveNic
    {
        public int Index { get; set; }
        public string MacAddress { get; set; }

        /// <summary>
        /// Proxmox supports several "models" for network devices (e.g. virtio, Intel E1000, VMWare vmxnet3, etc.)
        /// Proxmox stores the model along with the current network bridge the device is using in the format
        /// {MODEL=MAC_ADDRESS,BRIDGE=PROXMOX_BRIDGE_ID} on the Extension properties of the VM.
        /// </summary>
        public string PveModel { get; set; }
    }
}
