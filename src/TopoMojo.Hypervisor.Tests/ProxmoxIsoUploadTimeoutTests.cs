using System;
using System.Threading;
using TopoMojo.Hypervisor;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ProxmoxIsoUploadTimeoutTests
{
    [Theory]
    [InlineData(7, 7)]
    [InlineData(0, HypervisorServiceConfiguration.DefaultUploadTimeoutMinutes)]
    [InlineData(-5, HypervisorServiceConfiguration.DefaultUploadTimeoutMinutes)]
    public void ResolveIsoUploadTimeout_UsesConfiguredPositiveTimeoutOrDefault(
        int configuredMinutes,
        int expectedMinutes)
    {
        Assert.Equal(
            TimeSpan.FromMinutes(expectedMinutes),
            ProxmoxClient.ResolveIsoUploadTimeout(TimeSpan.FromMinutes(configuredMinutes)));
    }

    [Fact]
    public void ResolveIsoUploadTimeout_UsesDefaultForInfiniteTimeout()
    {
        Assert.Equal(
            TimeSpan.FromMinutes(HypervisorServiceConfiguration.DefaultUploadTimeoutMinutes),
            ProxmoxClient.ResolveIsoUploadTimeout(Timeout.InfiniteTimeSpan));
    }
}
