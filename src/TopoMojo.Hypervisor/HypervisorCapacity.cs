// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;

namespace TopoMojo.Hypervisor
{
    /// <summary>
    /// Point-in-time memory, cpu and disk figures for the compute and storage a hypervisor
    /// deploys to. Memory is the figure that decides how many gamespaces fit; cpu and disk
    /// are reported for information, because vcpus are overcommitted as a matter of course
    /// and linked clones make a gamespace's disk use unpredictable.
    /// </summary>
    public class HypervisorCapacity
    {
        public string Provider { get; set; }
        public DateTimeOffset AsOf { get; set; }
        public HypervisorCapacityNode[] Nodes { get; set; } = [];

        /// <summary>
        /// The storage named by Pod:VmStore and Pod:DiskStore. Shared storage appears once;
        /// local storage appears once per node that has it.
        /// </summary>
        public HypervisorCapacityStorage[] Storage { get; set; } = [];

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

    public class HypervisorCapacityStorage
    {
        public string Name { get; set; }

        /// <summary>
        /// The node that owns local storage. Null for shared storage.
        /// </summary>
        public string Node { get; set; }
        public bool Shared { get; set; }
        public bool Available { get; set; }
        public long TotalMb { get; set; }
        public long UsedMb { get; set; }
    }
}
