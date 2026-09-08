using System;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using TopoMojo.Hypervisor.Exceptions;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ProxmoxIsoStorageNodeTests
{
    [Fact]
    public void SelectIsoStorageNode_UsesAvailableNode()
    {
        var resources = new[]
        {
            StorageResource("iso", "offline", false, true),
            StorageResource("iso", "online", true, true)
        };

        var node = ProxmoxClient.SelectIsoStorageNode(resources, "iso", Random.Shared);

        Assert.Equal("online", node);
    }

    [Fact]
    public void SelectIsoStorageNode_RejectsWhenAllMatchingNodesAreUnavailable()
    {
        var resources = new[]
        {
            StorageResource("iso", "node-a", false, true),
            StorageResource("iso", "node-b", false, true)
        };

        Assert.Throws<HypervisorException>(
            () => ProxmoxClient.SelectIsoStorageNode(resources, "iso", Random.Shared));
    }

    [Fact]
    public void SelectIsoStorageNode_NeverSelectsAnUnavailableNode()
    {
        var resources = new[]
        {
            StorageResource("iso", "node-a", true, true),
            StorageResource("iso", "node-b", true, true),
            StorageResource("iso", "offline", false, true)
        };

        var node = ProxmoxClient.SelectIsoStorageNode(resources, "iso", Random.Shared);

        Assert.Contains(node, new[] { "node-a", "node-b" });
    }

    [Fact]
    public void SelectIsoStorageNode_RejectsAvailableNonSharedStorageOnMultipleNodes()
    {
        var resources = new[]
        {
            StorageResource("iso", "node-a", true, true),
            StorageResource("iso", "node-b", true, false)
        };

        Assert.Throws<HypervisorException>(
            () => ProxmoxClient.SelectIsoStorageNode(resources, "iso", Random.Shared));
    }

    [Fact]
    public void SelectIsoStorageNode_IgnoresOtherStoragesAndMissingNodeNames()
    {
        var resources = new[]
        {
            StorageResource("other", "other-node", true, true),
            StorageResource("iso", null, true, true),
            StorageResource("iso", " ", true, true),
            StorageResource("iso", "iso-node", true, true),
            new ClusterResource
            {
                ResourceType = ClusterResourceType.Vm,
                Storage = "iso",
                Node = "vm-node",
                IsAvailable = true,
                Shared = true
            }
        };

        var node = ProxmoxClient.SelectIsoStorageNode(resources, "iso", Random.Shared);

        Assert.Equal("iso-node", node);
    }

    private static ClusterResource StorageResource(
        string storage,
        string node,
        bool isAvailable,
        bool shared)
        => new()
        {
            ResourceType = ClusterResourceType.Storage,
            Storage = storage,
            Node = node,
            IsAvailable = isAvailable,
            Shared = shared
        };
}
