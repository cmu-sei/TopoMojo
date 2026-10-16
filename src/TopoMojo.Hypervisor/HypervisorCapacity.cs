// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;

namespace TopoMojo.Hypervisor
{
    /// <summary>
    /// Point-in-time memory and cpu figures for the compute a hypervisor deploys to.
    /// Memory is the figure that decides how many gamespaces fit; cpu is reported for
    /// information, because vcpus are overcommitted as a matter of course.
    /// </summary>
    public class HypervisorCapacity
    {
        public string Provider { get; set; }
        public DateTimeOffset AsOf { get; set; }
        public HypervisorCapacityNode[] Nodes { get; set; } = [];

        /// <summary>
        /// Memory still available to the TopoMojo resource pool when the pool has a limit
        /// (vSphere). Null when the pool is unlimited or the hypervisor has no pools.
        /// </summary>
        public long? PoolMemoryFreeMb { get; set; }
    }

    public class HypervisorCapacityNode
    {
        public string Name { get; set; }

        /// <summary>
        /// False for nodes that are offline, disconnected, or in maintenance. Their
        /// figures are reported but never counted as free.
        /// </summary>
        public bool Available { get; set; }
        public long MemoryTotalMb { get; set; }
        public long MemoryUsedMb { get; set; }
        public int CpuCores { get; set; }
        public double CpuUsagePercent { get; set; }
    }
}
