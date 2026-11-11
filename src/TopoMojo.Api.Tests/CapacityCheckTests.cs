// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

/// <summary>
/// CapacityCalculator.Check: whether one more deploy fits.
/// </summary>
public class CapacityCheckTests
{
    internal static HypervisorCapacityNode Node(string name, long totalMb, long usedMb, bool available = true) => new()
    {
        Name = name, Available = available, MemoryTotalMb = totalMb, MemoryUsedMb = usedMb, CpuCores = 8
    };

    internal static HypervisorCapacity Capacity(long? poolFreeMb, params HypervisorCapacityNode[] nodes) => new()
    {
        Provider = "test", AsOf = DateTimeOffset.UtcNow, Nodes = nodes, PoolMemoryFreeMb = poolFreeMb
    };

    internal static WorkspaceDemand Demand(long memoryMb) => new() { VmCount = 1, MemoryMb = memoryMb, Vcpu = 1 };

    [Fact]
    public void Fits_WhenFreeAfterHeadroomCoversTheDeploy()
    {
        // 10240 total, 20% headroom = 8192 usable, 2048 used = 6144 free
        var check = CapacityCalculator.Check(Demand(6144), Capacity(null, Node("a", 10240, 2048)), 0, 20, false);

        Assert.True(check.Fits);
        Assert.Equal(6144, check.FreeMb);
        Assert.Null(check.Message);
    }

    [Fact]
    public void DoesNotFit_WhenHeadroomTakesTheRoom()
    {
        // 7000 free without headroom, 6144 with it
        var check = CapacityCalculator.Check(Demand(7000), Capacity(null, Node("a", 10240, 2048)), 0, 20, false);

        Assert.False(check.Fits);
        Assert.Equal("memory", check.LimitedBy);
        Assert.Equal(7000, check.NeededMb);
        Assert.Contains("6.8 GB", check.Message);
        Assert.Contains("6.0 GB is free after 20% headroom", check.Message);
    }

    [Fact]
    public void ReservedMemoryCountsAgainstFree()
    {
        var capacity = Capacity(null, Node("a", 10240, 0));

        Assert.True(CapacityCalculator.Check(Demand(4096), capacity, 4096, 0, false).Fits);

        var check = CapacityCalculator.Check(Demand(4096), capacity, 8192, 0, false);
        Assert.False(check.Fits);
        Assert.Equal(2048, check.FreeMb);
        Assert.Equal(8192, check.ReservedMb);
    }

    [Fact]
    public void PoolLimitRefusesWhenTighterThanTheHosts()
    {
        var check = CapacityCalculator.Check(Demand(4096), Capacity(3072, Node("a", 102400, 0)), 0, 0, false);

        Assert.False(check.Fits);
        Assert.Equal("pool", check.LimitedBy);
        Assert.Equal(3072, check.FreeMb);
        Assert.Contains("resource pool", check.Message);
    }

    [Fact]
    public void PoolLimitAlsoLosesTheReservedMemory()
    {
        var capacity = Capacity(8192, Node("a", 102400, 0));

        Assert.True(CapacityCalculator.Check(Demand(4096), capacity, 4096, 0, false).Fits);
        Assert.Equal("pool", CapacityCalculator.Check(Demand(4096), capacity, 6144, 0, false).LimitedBy);
    }

    [Fact]
    public void HostAffinityNeedsOneHostWithRoom()
    {
        var capacity = Capacity(null, Node("a", 3072, 0), Node("b", 3072, 0));

        Assert.True(CapacityCalculator.Check(Demand(4096), capacity, 0, 0, false).Fits);

        var check = CapacityCalculator.Check(Demand(4096), capacity, 0, 0, true);
        Assert.False(check.Fits);
        Assert.Equal("host", check.LimitedBy);
        Assert.Contains("3.0 GB", check.Message);
    }

    [Fact]
    public void UnavailableHostsGiveNoRoom()
    {
        var check = CapacityCalculator.Check(Demand(1024),
            Capacity(null, Node("down", 102400, 0, available: false)), 0, 0, false);

        Assert.False(check.Fits);
    }

    [Fact]
    public void NoHostsReportedNeverRefuses()
    {
        Assert.True(CapacityCalculator.Check(Demand(1024), Capacity(null), 0, 20, false).Fits);
    }

    [Fact]
    public void NothingToDeployAlwaysFits()
    {
        Assert.True(CapacityCalculator.Check(new WorkspaceDemand(), Capacity(null, Node("a", 1024, 1024)), 0, 20, true).Fits);
    }

    [Fact]
    public void DemandOfVmTemplatesCountsEachVmOnceWithZeroRamAsOneGb()
    {
        var demand = CapacityCalculator.Demand(new VmTemplate[]
        {
            new() { Ram = 4, Cpu = "2x2" },
            new() { Ram = 0, Cpu = "" }
        });

        Assert.Equal(2, demand.VmCount);
        Assert.Equal(5 * 1024, demand.MemoryMb);
        Assert.Equal(5, demand.Vcpu);
    }
}

/// <summary>
/// CapacityGuard: Off/Warn/Block and the in-flight reservations.
/// </summary>
public class CapacityGuardTests
{
    private sealed class Rig
    {
        public Rig(CapacityCheckMode mode, HypervisorCapacity capacity)
        {
            Pod = DeployHypervisor.Create(capacity);
            Cache = new MemoryCache(new MemoryCacheOptions());
            Options = new CoreOptions { CapacityCheck = mode, CapacityHeadroomPercent = 0, CapacityCacheSeconds = 300 };
            Guard = new CapacityGuard(Pod, Cache, Options, NullLogger<CapacityGuard>.Instance);
        }

        public IHypervisorService Pod { get; }
        public DeployHypervisor Fake => (DeployHypervisor)(object)Pod;
        public MemoryCache Cache { get; }
        public CoreOptions Options { get; }
        public CapacityGuard Guard { get; }

        // a newer hypervisor read, as when the cache expires
        public void NewSnapshot(HypervisorCapacity capacity)
        {
            Fake.Capacity = capacity;
            Cache.Remove(HypervisorCapacityCache.Key);
        }
    }

    private static HypervisorCapacity TenGb() => CapacityCheckTests.Capacity(null, CapacityCheckTests.Node("a", 10240, 0));

    [Fact]
    public async Task Off_NeverReadsTheHypervisor()
    {
        var rig = new Rig(CapacityCheckMode.Off, TenGb());

        var r = await rig.Guard.Reserve("x", CapacityCheckTests.Demand(999999), false);

        Assert.Same(CapacityReservation.None, r);
        Assert.Equal(0, rig.Fake.CapacityReads);
    }

    [Fact]
    public async Task Warn_DeploysWithAWarningWhenItDoesNotFit()
    {
        var rig = new Rig(CapacityCheckMode.Warn, TenGb());

        Assert.Null((await rig.Guard.Reserve("x", CapacityCheckTests.Demand(4096), false)).Warning);

        var r = await rig.Guard.Reserve("y", CapacityCheckTests.Demand(20480), false);
        Assert.Contains("Not enough hypervisor memory", r.Warning);
    }

    [Fact]
    public async Task Block_RefusesWhatDoesNotFit()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());

        var ex = await Assert.ThrowsAsync<CapacityExceeded>(
            () => rig.Guard.Reserve("x", CapacityCheckTests.Demand(20480), false));

        Assert.Contains("needs 20.0 GB", ex.Message);
        Assert.Equal(0, rig.Guard.ReservedMb(DateTimeOffset.MinValue));
    }

    [Fact]
    public async Task Block_LetsAnOverrideThroughWithAWarning()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());

        var r = await rig.Guard.Reserve("x", CapacityCheckTests.Demand(20480), false, allowOverride: true);

        Assert.Contains("Deployed anyway because an administrator started it", r.Warning);
    }

    [Fact]
    public async Task Block_UsesTheHeadroom()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());
        rig.Options.CapacityHeadroomPercent = 50;

        await Assert.ThrowsAsync<CapacityExceeded>(() => rig.Guard.Reserve("x", CapacityCheckTests.Demand(6144), false));
    }

    [Fact]
    public async Task Block_UsesThePoolLimit()
    {
        var rig = new Rig(CapacityCheckMode.Block, CapacityCheckTests.Capacity(2048, CapacityCheckTests.Node("a", 102400, 0)));

        var ex = await Assert.ThrowsAsync<CapacityExceeded>(() => rig.Guard.Reserve("x", CapacityCheckTests.Demand(4096), false));
        Assert.Contains("resource pool", ex.Message);
    }

    [Fact]
    public async Task InFlightDeploysCountAgainstTheCachedSnapshot()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());

        // two 4 GB deploys fit in 10 GB; the third doesn't, though the snapshot still says 10 GB free
        await rig.Guard.Reserve("a", CapacityCheckTests.Demand(4096), false);
        await rig.Guard.Reserve("b", CapacityCheckTests.Demand(4096), false);
        await Assert.ThrowsAsync<CapacityExceeded>(() => rig.Guard.Reserve("c", CapacityCheckTests.Demand(4096), false));

        Assert.Equal(1, rig.Fake.CapacityReads);
    }

    [Fact]
    public async Task ConcurrentChecksCannotBothTakeTheLastRoom()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());

        var attempts = Enumerable.Range(0, 8)
            .Select(i => Task.Run(async () =>
            {
                try { await rig.Guard.Reserve($"g{i}", CapacityCheckTests.Demand(4096), false); return true; }
                catch (CapacityExceeded) { return false; }
            }))
            .ToArray();

        bool[] results = await Task.WhenAll(attempts);

        Assert.Equal(2, results.Count(ok => ok));
    }

    [Fact]
    public async Task AFailedDeployGivesItsMemoryBack()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());

        var r = await rig.Guard.Reserve("a", CapacityCheckTests.Demand(8192), false);
        r.Release();

        Assert.Null((await rig.Guard.Reserve("b", CapacityCheckTests.Demand(8192), false)).Warning);
    }

    [Fact]
    public async Task AFinishedDeployCountsUntilANewerSnapshot()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());

        var r = await rig.Guard.Reserve("a", CapacityCheckTests.Demand(8192), false);
        r.Complete();

        // the cached snapshot predates the deploy, so its memory still counts
        await Assert.ThrowsAsync<CapacityExceeded>(() => rig.Guard.Reserve("b", CapacityCheckTests.Demand(8192), false));

        // a snapshot read after the deploy finished shows the vms itself (here: 8 GB used)
        rig.NewSnapshot(CapacityCheckTests.Capacity(null, CapacityCheckTests.Node("a", 10240, 8192)));
        Assert.Equal(0, rig.Guard.ReservedMb(DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<CapacityExceeded>(() => rig.Guard.Reserve("c", CapacityCheckTests.Demand(4096), false));
        Assert.Null((await rig.Guard.Reserve("d", CapacityCheckTests.Demand(2048), false)).Warning);
    }

    [Fact]
    public async Task ADeployStillRunningCountsEvenAgainstANewerSnapshot()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());

        await rig.Guard.Reserve("a", CapacityCheckTests.Demand(8192), false);

        rig.NewSnapshot(TenGb());

        await Assert.ThrowsAsync<CapacityExceeded>(() => rig.Guard.Reserve("b", CapacityCheckTests.Demand(4096), false));
    }

    [Fact]
    public async Task AnUnreadableHypervisorDoesNotRefuse()
    {
        var rig = new Rig(CapacityCheckMode.Block, TenGb());
        rig.Fake.FailCapacity = true;

        var r = await rig.Guard.Reserve("a", CapacityCheckTests.Demand(999999), false);

        Assert.Same(CapacityReservation.None, r);
    }
}

/// <summary>
/// A hypervisor that reports a set capacity and remembers what it deployed.
/// </summary>
public class DeployHypervisor : NullHypervisor
{
    public HypervisorCapacity Capacity { get; set; }
    public bool FailCapacity { get; set; }
    public bool FailDeploy { get; set; }
    public int CapacityReads { get; private set; }
    public int Deploys { get; private set; }
    public List<Vm> Vms { get; } = [];

    public static IHypervisorService Create(HypervisorCapacity capacity)
    {
        var pod = DispatchProxy.Create<IHypervisorService, DeployHypervisor>();
        ((DeployHypervisor)(object)pod).Capacity = capacity;
        return pod;
    }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        switch (targetMethod.Name)
        {
            case nameof(IHypervisorService.GetCapacity):
                CapacityReads += 1;
                return FailCapacity
                    ? Task.FromException<HypervisorCapacity>(new InvalidOperationException("unreachable"))
                    : Task.FromResult(Capacity);

            case nameof(IHypervisorService.Find):
                string term = (string)args[0];
                return Task.FromResult(Vms.Where(v => v.Name.Contains(term)).ToArray());

            case nameof(IHypervisorService.Deploy) when args[0] is VmTemplate template:
                Deploys += 1;
                if (FailDeploy)
                    return Task.FromException<Vm>(new InvalidOperationException("deploy failed"));
                var vm = new Vm { Id = Guid.NewGuid().ToString(), Name = $"{template.Name}#{template.IsolationTag}" };
                Vms.Add(vm);
                return Task.FromResult(vm);

            case nameof(IHypervisorService.Deploy) when args[0] is DeploymentContext ctx:
                Deploys += 1;
                if (FailDeploy)
                    return Task.FromException(new InvalidOperationException("deploy failed"));
                foreach (var t in ctx.Templates.Where(t => !Vms.Any(v => v.Name == $"{t.Name}#{t.IsolationTag}")))
                    Vms.Add(new Vm { Id = Guid.NewGuid().ToString(), Name = $"{t.Name}#{t.IsolationTag}" });
                return Task.CompletedTask;
        }

        return base.Invoke(targetMethod, args);
    }
}
