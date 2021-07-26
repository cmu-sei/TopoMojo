// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;

namespace TopoMojo.Hypervisor
{
    public class VmTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string TopoId { get; set; }
        public string Cpu { get; set; }
        public string Guest { get; set; }
        public string Source { get; set; }
        public string Iso { get; set; }
        public string Floppy { get; set; }
        public string Version { get; set; }
        public string IsolationTag { get; set; }
        public bool HostAffinity {get; set; }
        public bool UseUplinkSwitch {get; set; }
        public int Ram { get; set; }
        public int VideoRam { get; set; }
        public int Adapters { get; set; }
        public int Delay { get; set; }
        public bool AutoStart { get; set; } = true;
        public VmNet[] Eth { get; set; }
        public VmDisk[] Disks { get; set; }
        public VmKeyValue[] GuestSettings { get; set; }
    }

    public class VmNet
    {
        public int Id { get; set; }
        public string Net { get; set; }
        public string Key { get; set; }
        public string Type { get; set; }
        public string Mac { get; set; }
        public string Ip { get; set; }
        public int Vlan { get; set; }
    }

    public class VmDisk
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public string Source { get; set; }
        public string Controller { get; set; }
        public int Size { get; set; }
        public int Status { get; set; }
    }

    public class VmKeyValue
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
