// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

public class CapacityCalculatorTests
{
    private static ConvergedTemplate Vm(int ram, string cpu, int replicas = 0, int variant = 0) => new()
    {
        Detail = $"{{\"ram\":{ram},\"cpu\":\"{cpu}\"}}",
        Replicas = replicas,
        Variant = variant
    };

    private static HypervisorCapacityNode Node(string name, long totalMb, long usedMb, bool available = true) => new()
    {
        Name = name, Available = available, MemoryTotalMb = totalMb, MemoryUsedMb = usedMb, CpuCores = 8, CpuUsagePercent = 50
    };

    private static HypervisorCapacity Capacity(long? poolFreeMb, params HypervisorCapacityNode[] nodes) => new()
    {
        Provider = "test", AsOf = DateTimeOffset.UnixEpoch, Nodes = nodes, PoolMemoryFreeMb = poolFreeMb
    };

    private static WorkspaceDemand Demand(long memoryMb) => new() { VmCount = 1, MemoryMb = memoryMb, Vcpu = 1 };

    [Fact]
    public void Demand_SumsRamInMbAndSocketsTimesCores()
    {
        var demand = CapacityCalculator.Demand([Vm(4, "1x2"), Vm(2, "2x2")], players: 1, replicaLimit: 5);

        Assert.Equal(2, demand.VmCount);
        Assert.Equal(6 * 1024, demand.MemoryMb);
        Assert.Equal(6, demand.Vcpu);
    }

    [Fact]
    public void Demand_ZeroRamDeploysAsOneGb()
    {
        var demand = CapacityCalculator.Demand([Vm(0, "1")], players: 1, replicaLimit: 5);

        Assert.Equal(1024, demand.MemoryMb);
    }

    [Theory]
    [InlineData(3, 1, 5, 3)]   // fixed replicas
    [InlineData(9, 1, 5, 5)]   // capped at the replica limit
    [InlineData(-1, 4, 5, 4)]  // follows the player count
    [InlineData(-1, 9, 5, 9)]  // player count is not capped
    [InlineData(0, 1, 5, 1)]   // always at least one vm
    public void Demand_ExpandsReplicasLikeDeploy(int replicas, int players, int limit, int expectedVms)
    {
        var demand = CapacityCalculator.Demand([Vm(1, "1", replicas)], players, limit);

        Assert.Equal(expectedVms, demand.VmCount);
        Assert.Equal(expectedVms * 1024, demand.MemoryMb);
    }

    [Fact]
    public void Demand_SizesByTheLargestVariantPlusCommonTemplates()
    {
        var demand = CapacityCalculator.Demand(
            [Vm(2, "1", variant: 0), Vm(4, "1", variant: 1), Vm(8, "1", variant: 2)],
            players: 1, replicaLimit: 5);

        Assert.Equal(2, demand.VmCount);
        Assert.Equal(10 * 1024, demand.MemoryMb);
    }

    [Theory]
    [InlineData("2x4", 8)]
    [InlineData("4", 4)]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    [InlineData("junk", 1)]
    public void Vcpu_ParsesSocketsByCores(string cpu, int expected)
    {
        Assert.Equal(expected, CapacityCalculator.Vcpu(cpu));
    }

    [Fact]
    public void Fit_HoldsBackHeadroomOnEachHost()
    {
        // 10240 total, 20% headroom = 8192 usable, 2048 used = 6144 free = 3 x 2048
        var fit = CapacityCalculator.Fit(Demand(2048), Capacity(null, Node("a", 10240, 2048)), 3, 20, false);

        Assert.Equal(6144, fit.MemoryFreeMb);
        Assert.Equal(3, fit.Fits);
        Assert.True(fit.Sufficient);
        Assert.Equal("memory", fit.LimitedBy);
    }

    [Fact]
    public void Fit_InsufficientWhenFewerFitThanRequested()
    {
        var fit = CapacityCalculator.Fit(Demand(2048), Capacity(null, Node("a", 10240, 2048)), 4, 20, false);

        Assert.Equal(3, fit.Fits);
        Assert.False(fit.Sufficient);
    }

    [Fact]
    public void Fit_UsedPastHeadroomCountsAsZeroNotNegative()
    {
        var fit = CapacityCalculator.Fit(Demand(1024),
            Capacity(null, Node("a", 10240, 10000), Node("b", 10240, 0)), 1, 20, false);

        Assert.Equal(8192, fit.MemoryFreeMb);
        Assert.Equal(8, fit.Fits);
    }

    [Fact]
    public void Fit_IgnoresUnavailableHosts()
    {
        var fit = CapacityCalculator.Fit(Demand(1024),
            Capacity(null, Node("a", 10240, 0), Node("b", 10240, 0, available: false)), 1, 0, false);

        Assert.Equal(10, fit.Fits);
        Assert.Equal(10240, fit.MemoryTotalMb);
        Assert.Equal(8, fit.CpuCores);
    }

    [Fact]
    public void Fit_PoolLimitWinsWhenTighter()
    {
        var fit = CapacityCalculator.Fit(Demand(1024), Capacity(3072, Node("a", 10240, 0)), 1, 0, false);

        Assert.Equal(3072, fit.MemoryFreeMb);
        Assert.Equal(3, fit.Fits);
        Assert.Equal("pool", fit.LimitedBy);
    }

    [Fact]
    public void Fit_HostAffinityNeedsTheWholeGamespaceOnOneHost()
    {
        // two hosts with 3 GB free each: 6 GB total, but a 4 GB gamespace fits on neither
        var capacity = Capacity(null, Node("a", 3072, 0), Node("b", 3072, 0));

        Assert.Equal(1, CapacityCalculator.Fit(Demand(4096), capacity, 1, 0, false).Fits);
        Assert.Equal(0, CapacityCalculator.Fit(Demand(4096), capacity, 1, 0, true).Fits);
    }

    [Fact]
    public void Fit_NoTemplatesIsUnlimited()
    {
        var fit = CapacityCalculator.Fit(new WorkspaceDemand(), Capacity(null, Node("a", 1024, 1024)), 5, 20, false);

        Assert.Null(fit.Fits);
        Assert.Null(fit.LimitedBy);
        Assert.True(fit.Sufficient);
    }

    [Fact]
    public void Fit_ClampsHeadroom()
    {
        var fit = CapacityCalculator.Fit(Demand(1024), Capacity(null, Node("a", 10240, 0)), 1, 150, false);

        Assert.Equal(90, fit.HeadroomPercent);
        Assert.Equal(1, fit.Fits);
    }
}
