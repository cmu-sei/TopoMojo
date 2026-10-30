using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using TopoMojo.Hypervisor.Exceptions;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ProxmoxCreateTemplateTests
{
    [Fact]
    public void HasVmNamed_FindsACloneStillInProgress()
    {
        // Until the clone finishes it isn't a template, which is why the vm cache leaves it out.
        var resources = new[]
        {
            new ClusterResource { VmId = 100, Name = "other" },
            new ClusterResource { VmId = 101, Name = "3f2c0b8e-clone", IsTemplate = false }
        };

        Assert.True(ProxmoxClient.HasVmNamed(resources, "3f2c0b8e-clone"));
    }

    [Fact]
    public void HasVmNamed_IgnoresOtherAndUnnamedVms()
    {
        var resources = new[]
        {
            new ClusterResource { VmId = 100, Name = "other" },
            new ClusterResource { VmId = 101, Name = null }
        };

        Assert.False(ProxmoxClient.HasVmNamed(resources, "3f2c0b8e-clone"));
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
