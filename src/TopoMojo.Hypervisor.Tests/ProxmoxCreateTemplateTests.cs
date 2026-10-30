using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using TopoMojo.Hypervisor.Exceptions;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ProxmoxCreateTemplateTests
{
    [Fact]
    public void TemplateExists_FindsACloneStillInProgress()
    {
        // Until the clone finishes it isn't a template, which is why the vm cache leaves it out.
        var resources = new[]
        {
            new ClusterResource { VmId = 100, Name = "other" },
            new ClusterResource { VmId = 101, Name = "3f2c0b8e-clone", IsTemplate = false }
        };

        Assert.True(ProxmoxClient.TemplateExists([], resources, "3f2c0b8e-clone"));
    }

    [Fact]
    public void TemplateExists_IgnoresOtherAndUnnamedVms()
    {
        var resources = new[]
        {
            new ClusterResource { VmId = 100, Name = "other" },
            new ClusterResource { VmId = 101, Name = null }
        };

        Assert.False(ProxmoxClient.TemplateExists([], resources, "3f2c0b8e-clone"));
    }

    [Fact]
    public void TemplateExists_FindsACloneThatJustFinished()
    {
        // Proxmox lists a new vm several seconds after the clone task ends; the cache already has it.
        var cached = new[] { new Vm { Id = "101", Name = "3f2c0b8e-clone" } };
        var resources = new[] { new ClusterResource { VmId = 100, Name = "other" } };

        Assert.True(ProxmoxClient.TemplateExists(cached, resources, "3f2c0b8e-clone"));
    }

    [Fact]
    public void EnsureFinished_ThrowsWhenTheTaskTimedOut()
    {
        var ex = Assert.Throws<HypervisorException>(
            () => ProxmoxClient.EnsureFinished(false, "clone", "3f2c0b8e-clone"));

        Assert.Contains("clone", ex.Message);
        Assert.Contains("3f2c0b8e-clone", ex.Message);
    }

    [Fact]
    public void EnsureFinished_AcceptsAFinishedTask()
    {
        ProxmoxClient.EnsureFinished(true, "clone", "3f2c0b8e-clone");
    }
}
