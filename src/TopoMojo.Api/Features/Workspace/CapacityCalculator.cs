// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Models;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Services
{
    /// <summary>
    /// Works out what one gamespace of a workspace needs and how many fit in what the
    /// hypervisor has free. Advisory: nothing is reserved, so two callers can both be
    /// told the same capacity is free.
    /// </summary>
    public static class CapacityCalculator
    {
        /// <summary>
        /// Memory and vcpus for one gamespace, sized the way a deploy sizes it: ram in GB
        /// (0 deploys as 1 GB), cpu as "sockets x cores", replicas expanded and capped
        /// at the replica limit. A workspace with variants is sized by its largest one.
        /// </summary>
        public static WorkspaceDemand Demand(IEnumerable<ConvergedTemplate> templates, int players, int replicaLimit)
        {
            var list = templates.ToArray();

            var variants = list
                .Where(t => t.Variant > 0)
                .Select(t => t.Variant)
                .Distinct()
                .DefaultIfEmpty(0);

            return variants
                .Select(v => Sum(
                    list.Where(t => t.Variant == 0 || t.Variant == v),
                    players,
                    replicaLimit))
                .OrderByDescending(d => d.MemoryMb)
                .ThenByDescending(d => d.Vcpu)
                .First();
        }

        /// <summary>
        /// Memory and vcpus for vms that are about to deploy, one vm per template (replicas
        /// already expanded). Ram 0 counts as 1 GB, the way it deploys.
        /// </summary>
        public static WorkspaceDemand Demand(IEnumerable<VmTemplate> vms)
        {
            var demand = new WorkspaceDemand();

            foreach (var vm in vms)
            {
                demand.VmCount += 1;
                demand.MemoryMb += (long)(vm.Ram > 0 ? vm.Ram : 1) * 1024;
                demand.Vcpu += Vcpu(vm.Cpu);
            }

            return demand;
        }

        /// <summary>
        /// Whether one more deploy of this size fits, after headroom on each host and after
        /// memory already promised to deploys that the snapshot doesn't show yet.
        /// </summary>
        /// <remarks>
        /// The pool limit and the hosts' total are both reduced by the reserved memory.
        /// With host affinity the deploy also has to fit whole on one host; reserved memory
        /// isn't taken off any one host, since which host an in-flight deploy landed on
        /// isn't known.
        /// A snapshot with no hosts (the hypervisor reported nothing) never refuses.
        /// </remarks>
        public static CapacityCheckResult Check(
            WorkspaceDemand demand,
            HypervisorCapacity capacity,
            long reservedMb,
            int headroomPercent,
            bool hostAffinity)
        {
            headroomPercent = Math.Clamp(headroomPercent, 0, 90);
            reservedMb = Math.Max(0, reservedMb);

            var free = capacity.Nodes
                .Select(n => NodeFreeMb(n, headroomPercent))
                .ToArray();

            long hostFreeMb = Math.Max(0, free.Sum() - reservedMb);
            long? poolFreeMb = capacity.PoolMemoryFreeMb.HasValue
                ? Math.Max(0, capacity.PoolMemoryFreeMb.Value - reservedMb)
                : null;
            long largestHostMb = free.DefaultIfEmpty(0).Max();

            var result = new CapacityCheckResult
            {
                Fits = true,
                NeededMb = demand.MemoryMb,
                FreeMb = poolFreeMb.HasValue ? Math.Min(hostFreeMb, poolFreeMb.Value) : hostFreeMb,
                ReservedMb = reservedMb,
                HeadroomPercent = headroomPercent
            };

            if (demand.MemoryMb <= 0 || capacity.Nodes.Length == 0)
                return result;

            if (poolFreeMb.HasValue && poolFreeMb.Value < demand.MemoryMb && poolFreeMb.Value <= hostFreeMb)
            {
                result.Fits = false;
                result.LimitedBy = "pool";
                result.Message = $"Not enough memory left in the resource pool: this deploy needs {Gb(demand.MemoryMb)} GB "
                    + $"and the pool limit leaves {Gb(poolFreeMb.Value)} GB.";
            }
            else if (hostFreeMb < demand.MemoryMb)
            {
                result.Fits = false;
                result.LimitedBy = "memory";
                result.Message = $"Not enough hypervisor memory: this deploy needs {Gb(demand.MemoryMb)} GB "
                    + $"and {Gb(hostFreeMb)} GB is free after {headroomPercent}% headroom.";
            }
            else if (hostAffinity && largestHostMb < demand.MemoryMb)
            {
                result.Fits = false;
                result.LimitedBy = "host";
                result.Message = $"No single host has room: host affinity keeps this deploy's {Gb(demand.MemoryMb)} GB "
                    + $"on one host and the most any host has free is {Gb(largestHostMb)} GB after {headroomPercent}% headroom.";
            }

            return result;
        }

        /// <summary>
        /// Hosts and stores whose memory or storage use is at or over the warning level.
        /// Unavailable hosts and stores are left out; the dashboard marks them on its own.
        /// </summary>
        public static CapacityAlert[] Alerts(HypervisorCapacity capacity, int warnPercent, int criticalPercent)
        {
            (warnPercent, criticalPercent) = Thresholds(warnPercent, criticalPercent);

            var memory = capacity.Nodes
                .Where(n => n.Available)
                .Select(n => Alert(n.Name, null, "memory", n.MemoryUsedMb, n.MemoryTotalMb, warnPercent, criticalPercent));

            var storage = capacity.Storage
                .Where(s => s.Available)
                .Select(s => Alert(s.Name, s.Node, "storage", s.UsedMb, s.TotalMb, warnPercent, criticalPercent));

            return memory
                .Concat(storage)
                .Where(a => a is not null)
                .OrderByDescending(a => a.Level == CapacityAlert.Critical)
                .ThenByDescending(a => a.Percent)
                .ToArray();
        }

        /// <summary>
        /// Warning and critical levels kept within 1-100, with critical never below warning.
        /// </summary>
        public static (int warn, int critical) Thresholds(int warnPercent, int criticalPercent)
        {
            int warn = Math.Clamp(warnPercent, 1, 100);
            int critical = Math.Clamp(criticalPercent, warn, 100);
            return (warn, critical);
        }

        private static CapacityAlert Alert(string name, string node, string resource, long usedMb, long totalMb, int warn, int critical)
        {
            if (totalMb <= 0)
                return null;

            double percent = Math.Round(usedMb * 100.0 / totalMb, 1);

            if (percent < warn)
                return null;

            return new CapacityAlert
            {
                Name = name,
                Node = node,
                Resource = resource,
                Percent = percent,
                Level = percent >= critical ? CapacityAlert.Critical : CapacityAlert.Warning
            };
        }

        private static string Gb(long mb) => (mb / 1024.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        public static WorkspaceCapacity Fit(
            WorkspaceDemand demand,
            HypervisorCapacity capacity,
            int requested,
            int headroomPercent,
            bool hostAffinity)
        {
            headroomPercent = Math.Clamp(headroomPercent, 0, 90);

            var free = capacity.Nodes
                .Select(n => NodeFreeMb(n, headroomPercent))
                .ToArray();

            long hostFreeMb = free.Sum();
            long freeMb = capacity.PoolMemoryFreeMb.HasValue
                ? Math.Min(hostFreeMb, capacity.PoolMemoryFreeMb.Value)
                : hostFreeMb;

            int? fits = null;
            string limitedBy = null;

            if (demand.MemoryMb > 0)
            {
                // With host affinity every vm of a gamespace lands on one host, so a
                // gamespace has to fit whole into a single host's free memory.
                long hostFits = hostAffinity
                    ? free.Sum(f => f / demand.MemoryMb)
                    : hostFreeMb / demand.MemoryMb;

                long poolFits = capacity.PoolMemoryFreeMb.HasValue
                    ? capacity.PoolMemoryFreeMb.Value / demand.MemoryMb
                    : long.MaxValue;

                fits = (int)Math.Min(Math.Min(hostFits, poolFits), int.MaxValue);
                limitedBy = poolFits < hostFits ? "pool" : "memory";
            }

            var available = capacity.Nodes.Where(n => n.Available).ToArray();
            int cores = available.Sum(n => n.CpuCores);

            return new WorkspaceCapacity
            {
                Provider = capacity.Provider,
                AsOf = capacity.AsOf,
                Requested = requested,
                Fits = fits,
                Sufficient = fits is null || fits >= requested,
                LimitedBy = limitedBy,
                HeadroomPercent = headroomPercent,
                PerInstance = demand,
                MemoryFreeMb = freeMb,
                MemoryTotalMb = available.Sum(n => n.MemoryTotalMb),
                CpuCores = cores,
                CpuUsagePercent = cores > 0
                    ? Math.Round(available.Sum(n => n.CpuUsagePercent * n.CpuCores) / cores, 1)
                    : 0
            };
        }

        private static long NodeFreeMb(HypervisorCapacityNode node, int headroomPercent)
        {
            if (!node.Available)
                return 0;

            long usable = node.MemoryTotalMb * (100 - headroomPercent) / 100;

            return Math.Max(0, usable - node.MemoryUsedMb);
        }

        private static WorkspaceDemand Sum(IEnumerable<ConvergedTemplate> templates, int players, int replicaLimit)
        {
            var demand = new WorkspaceDemand();

            foreach (var template in templates)
            {
                var vm = new TemplateUtility(template.Detail).AsTemplate();

                // matches the replica expansion in GamespaceService.Deploy
                int replicas = template.Replicas < 0
                    ? Math.Max(players, 1)
                    : Math.Min(template.Replicas, replicaLimit);

                replicas = Math.Max(replicas, 1);

                demand.VmCount += replicas;
                demand.MemoryMb += replicas * (long)(vm.Ram > 0 ? vm.Ram : 1) * 1024;
                demand.Vcpu += replicas * Vcpu(vm.Cpu);
            }

            return demand;
        }

        // Same parse as the hypervisors: "sockets x cores", each part defaulting to 1.
        internal static int Vcpu(string cpu)
        {
            string[] p = (cpu ?? "").Split('x');

            if (!int.TryParse(p[0], out int sockets) || sockets < 1)
                sockets = 1;

            if (p.Length <= 1 || !int.TryParse(p[1], out int cores) || cores < 1)
                cores = 1;

            return sockets * cores;
        }
    }
}
