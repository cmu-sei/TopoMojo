using System;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;
using TopoMojo.Api;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class HypervisorHttpClientTimeoutTests
{
    [Theory]
    [InlineData(7, 7)]
    [InlineData(0, HypervisorServiceConfiguration.DefaultUploadTimeoutMinutes)]
    [InlineData(-5, HypervisorServiceConfiguration.DefaultUploadTimeoutMinutes)]
    public void AddTopoMojoHypervisor_ConfiguresStorageUploadClients(
        int configuredMinutes,
        int expectedMinutes)
    {
        var services = new ServiceCollection();
        services.AddTopoMojoHypervisor(
            () => new HypervisorServiceConfiguration(),
            new FileUploadOptions { UploadTimeoutMinutes = configuredMinutes });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        var expectedTimeout = TimeSpan.FromMinutes(expectedMinutes);

        Assert.Equal(expectedTimeout, factory.CreateClient("proxmoxIsoUpload").Timeout);
        Assert.Equal(expectedTimeout, factory.CreateClient("vSphereDatastore").Timeout);
    }
}
