using System.Net.Http;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor;
using TopoMojo.Hypervisor.Exceptions;
using TopoMojo.Hypervisor.Proxmox;
using TopoMojo.Hypervisor.vSphere;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class SubfolderLayoutTests
{
    private const string WorkspaceId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void VSphere_SupportsSubfolders_AndBuildsScopedPath()
    {
        var svc = new VSphereHypervisorService(
            new HypervisorServiceConfiguration { IsoStore = "[datastore] iso/" },
            new LoggerFactory(),
            new StubHttpClientFactory());

        Assert.True(svc.SupportsSubfolders);
        Assert.Equal(
            "[datastore] iso/0123456789abcdef0123456789abcdef/MyFile.iso",
            svc.GetIsoDatastorePath(WorkspaceId, "My File.iso"));
    }

    [Fact]
    public void Proxmox_BuildsScopedPathWithoutConstructingService()
    {
        Assert.Equal(
            "iso/0123456789abcdef0123456789abcdef/My File.iso",
            ProxmoxIsoNaming.BuildDatastorePath(
                "iso/", WorkspaceId, "sub/My File.iso"));
        Assert.Throws<HypervisorException>(
            () => ProxmoxIsoNaming.BuildDatastorePath("iso/", "not-a-guid", "f.iso"));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
