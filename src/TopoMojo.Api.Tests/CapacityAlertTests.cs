// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

public class CapacityAlertTests
{
    private static HypervisorCapacityNode Node(string name, long totalMb, long usedMb, bool available = true) => new()
    {
        Name = name, Available = available, MemoryTotalMb = totalMb, MemoryUsedMb = usedMb, CpuCores = 8, CpuUsagePercent = 99
    };

    private static HypervisorCapacityStorage Store(string name, long totalMb, long usedMb, string node = null, bool available = true) => new()
    {
        Name = name, Node = node, Shared = node is null, Available = available, TotalMb = totalMb, UsedMb = usedMb
    };

    private static HypervisorCapacity Capacity(HypervisorCapacityNode[] nodes, params HypervisorCapacityStorage[] storage) => new()
    {
        Provider = "test", AsOf = DateTimeOffset.UnixEpoch, Nodes = nodes, Storage = storage
    };

    [Fact]
    public void Alerts_LeavesHostsUnderTheWarningLevelOut()
    {
        var alerts = CapacityCalculator.Alerts(Capacity([Node("a", 100, 74)]), 75, 90);

        Assert.Empty(alerts);
    }

    [Theory]
    [InlineData(75, "warning")]
    [InlineData(89, "warning")]
    [InlineData(90, "critical")]
    [InlineData(100, "critical")]
    public void Alerts_LevelsMemoryAtTheThresholds(long used, string level)
    {
        var alert = Assert.Single(CapacityCalculator.Alerts(Capacity([Node("a", 100, used)]), 75, 90));

        Assert.Equal("a", alert.Name);
        Assert.Equal("memory", alert.Resource);
        Assert.Equal(level, alert.Level);
        Assert.Equal(used, alert.Percent);
    }

    [Fact]
    public void Alerts_CoverStorageAndNameTheOwningHost()
    {
        var alert = Assert.Single(CapacityCalculator.Alerts(
            Capacity([Node("a", 100, 10)], Store("local-lvm", 1000, 950, node: "a"), Store("nfs", 1000, 100)),
            75, 90));

        Assert.Equal("storage", alert.Resource);
        Assert.Equal("local-lvm", alert.Name);
        Assert.Equal("a", alert.Node);
        Assert.Equal("critical", alert.Level);
    }

    [Fact]
    public void Alerts_IgnoreCpuUnavailableHostsAndEmptyTotals()
    {
        var alerts = CapacityCalculator.Alerts(
            Capacity([Node("down", 100, 99, available: false), Node("empty", 0, 0)], Store("gone", 100, 99, available: false)),
            75, 90);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Alerts_ListCriticalFirstThenFullest()
    {
        var alerts = CapacityCalculator.Alerts(
            Capacity([Node("w80", 100, 80), Node("c91", 100, 91), Node("w85", 100, 85), Node("c95", 100, 95)]),
            75, 90);

        Assert.Equal(["c95", "c91", "w85", "w80"], alerts.Select(a => a.Name));
    }

    [Theory]
    [InlineData(75, 90, 75, 90)]
    [InlineData(0, 90, 1, 90)]
    [InlineData(150, 200, 100, 100)]
    [InlineData(80, 60, 80, 80)]   // critical never below warning
    public void Thresholds_AreClamped(int warn, int critical, int expectedWarn, int expectedCritical)
    {
        Assert.Equal((expectedWarn, expectedCritical), CapacityCalculator.Thresholds(warn, critical));
    }

    [Fact]
    public async Task Report_CarriesTheSnapshotThresholdsAndAlerts()
    {
        var capacity = Capacity([Node("a", 100, 92), Node("b", 100, 10)], Store("nfs", 100, 80));
        var service = new WorkspaceService(
            null, null, CapacityHypervisor.Create(capacity),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<WorkspaceService>.Instance, DocRootFixture.Mapper,
            new CoreOptions { CapacityWarnPercent = 75, CapacityCriticalPercent = 90 });

        var report = await service.GetHypervisorCapacityReport();

        Assert.Equal("test", report.Provider);
        Assert.Equal(2, report.Nodes.Length);
        Assert.Single(report.Storage);
        Assert.Equal(75, report.WarnPercent);
        Assert.Equal(90, report.CriticalPercent);
        Assert.Equal([("a", "critical"), ("nfs", "warning")], report.Alerts.Select(a => (a.Name, a.Level)));
    }
}

/// <summary>
/// A hypervisor that reports a fixed capacity snapshot and nothing else.
/// </summary>
public class CapacityHypervisor : NullHypervisor
{
    public HypervisorCapacity Capacity { get; set; }
    public int CapacityReads { get; private set; }

    public static IHypervisorService Create(HypervisorCapacity capacity)
    {
        var pod = DispatchProxy.Create<IHypervisorService, CapacityHypervisor>();
        ((CapacityHypervisor)(object)pod).Capacity = capacity;
        return pod;
    }

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        if (targetMethod.Name == nameof(IHypervisorService.GetCapacity))
        {
            CapacityReads += 1;
            return Task.FromResult(Capacity);
        }

        return base.Invoke(targetMethod, args);
    }
}
