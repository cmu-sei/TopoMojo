// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using VimClient;

namespace TopoMojo.Hypervisor.vSphere
{
    public class PortGroupAllocation
    {
        public string Net { get; set; }
        public string Key { get; set; }
        public int Counter { get; set; }
        public int VlanId { get; set; }
        public string Switch { get; set; }
        public DateTimeOffset Timestamp { get; set; }
    }

    public class VmNetwork
    {
        public string VmName { get; set; }
        public string NetworkMOR { get; set; }
    }

    internal class VimHostTask
    {
        public ManagedObjectReference Task { get; set; }
        public string Action { get; set; }
        public int Progress { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
    }
}
