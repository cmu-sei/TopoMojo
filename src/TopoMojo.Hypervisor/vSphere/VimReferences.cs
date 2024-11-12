// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using VimClient;

namespace TopoMojo.Hypervisor.vSphere
{
    public class VimReferences
    {
        public VimPortTypeClient Vim { get; set; }
        public ManagedObjectReference Cluster { get; set; }
        public ManagedObjectReference Props { get; set; }
        public ManagedObjectReference Pool { get; set; }
        public ManagedObjectReference VmFolder { get; set; }
        public ManagedObjectReference Dvs { get; set; }
        public ManagedObjectReference Net { get; set; }
        public string UplinkSwitch { get; set; }
        public string DvsUuid { get; set; }
        public string ExcludeNetworkMask { get; set; } = "topomojo";
        public string TenantId { get; set; }
        public string NetworkAllowAllSuffix { get; set; } = "-aa";
    }
}
