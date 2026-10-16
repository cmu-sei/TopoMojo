// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using TopoMojo.Hypervisor.Proxmox;
using VimClient;
using Xunit;
using Vim = TopoMojo.Hypervisor.vSphere.VimClient;

namespace TopoMojo.Hypervisor.Tests;

public sealed class CapacityMappingTests
{
    private const long Gb = 1024L * 1024 * 1024;

    [Fact]
    public void ProxmoxNodeMapsBytesToMbAndCpuFractionToPercent()
    {
        var node = ProxmoxClient.MapCapacityNode(new ClusterResource
        {
            ResourceType = ClusterResourceType.Node,
            Node = "pve1",
            IsOnline = true,
            MemorySize = (ulong)(64 * Gb),
            MemoryUsage = (ulong)(16 * Gb),
            CpuSize = 16,
            CpuUsagePercentage = 0.1234
        });

        Assert.Equal("pve1", node.Name);
        Assert.True(node.Available);
        Assert.Equal(65536, node.MemoryTotalMb);
        Assert.Equal(16384, node.MemoryUsedMb);
        Assert.Equal(16, node.CpuCores);
        Assert.Equal(12.3, node.CpuUsagePercent);
    }

    [Fact]
    public void ProxmoxOfflineNodeIsUnavailable()
    {
        var node = ProxmoxClient.MapCapacityNode(new ClusterResource
        {
            ResourceType = ClusterResourceType.Node, Node = "pve2", IsOnline = false
        });

        Assert.False(node.Available);
    }

    private static HostListSummary Summary(
        HostSystemConnectionState state = HostSystemConnectionState.connected, bool maintenance = false) => new()
    {
        hardware = new HostHardwareSummary { memorySize = 128 * Gb, numCpuCores = 20, cpuMhz = 2000 },
        quickStats = new HostListSummaryQuickStats { overallMemoryUsage = 32768, overallCpuUsage = 10000 },
        runtime = new HostRuntimeInfo { connectionState = state, inMaintenanceMode = maintenance }
    };

    [Fact]
    public void VSphereHostMapsMemoryAndCpuMhzToPercent()
    {
        var node = Vim.MapCapacityNode("esx1", Summary());

        Assert.True(node.Available);
        Assert.Equal(131072, node.MemoryTotalMb);
        Assert.Equal(32768, node.MemoryUsedMb);
        Assert.Equal(20, node.CpuCores);
        Assert.Equal(25, node.CpuUsagePercent);
    }

    [Theory]
    [InlineData(HostSystemConnectionState.disconnected, false)]
    [InlineData(HostSystemConnectionState.notResponding, false)]
    [InlineData(HostSystemConnectionState.connected, true)]
    public void VSphereHostIsUnavailableUnlessConnected(HostSystemConnectionState state, bool expected)
    {
        Assert.Equal(expected, Vim.MapCapacityNode("esx1", Summary(state)).Available);
    }

    [Fact]
    public void VSphereHostInMaintenanceIsUnavailable()
    {
        Assert.False(Vim.MapCapacityNode("esx1", Summary(maintenance: true)).Available);
    }

    [Fact]
    public void VSphereHostWithNoSummaryIsUnavailable()
    {
        var node = Vim.MapCapacityNode("esx1", null);

        Assert.False(node.Available);
        Assert.Equal(0, node.MemoryTotalMb);
    }

    private static ResourceConfigSpec Limit(long limitMb, bool specified = true) => new()
    {
        memoryAllocation = new ResourceAllocationInfo { limit = limitMb, limitSpecified = specified }
    };

    private static ResourcePoolRuntimeInfo Used(long bytes) => new()
    {
        memory = new ResourcePoolResourceUsage { overallUsage = bytes }
    };

    [Fact]
    public void VSpherePoolFreeIsLimitLessUsage()
    {
        Assert.Equal(6144, Vim.PoolMemoryFreeMb(Limit(8192), Used(2 * Gb)));
    }

    [Fact]
    public void VSpherePoolOverLimitIsZero()
    {
        Assert.Equal(0, Vim.PoolMemoryFreeMb(Limit(1024), Used(2 * Gb)));
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(8192, false)]
    public void VSpherePoolWithoutALimitIsNull(long limit, bool specified)
    {
        Assert.Null(Vim.PoolMemoryFreeMb(Limit(limit, specified), Used(Gb)));
    }
}
