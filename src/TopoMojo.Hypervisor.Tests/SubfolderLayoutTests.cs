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
    public void VSphere_UsesScopeFolders_AndBuildsScopedPath()
    {
        var svc = new VSphereHypervisorService(
            new HypervisorServiceConfiguration { IsoStore = "[datastore] iso/" },
            new LoggerFactory(),
            new StubHttpClientFactory());

        Assert.Equal(
            "0123456789abcdef0123456789abcdef/MyFile.iso",
            svc.GetIsoStorePath(WorkspaceId, "My File.iso"));
        Assert.Single(svc.GetIsoStorePathCandidates(WorkspaceId, "My File.iso"));
        Assert.Equal(
            "[datastore] iso/0123456789abcdef0123456789abcdef/MyFile.iso",
            svc.GetIsoDatastorePath(WorkspaceId, "My File.iso"));
    }

    [Fact]
    public void BuildDatastorePath_NormalizesFilename()
    {
        Assert.Equal(
            "iso/0123456789abcdef0123456789abcdef/foo._iso",
            ProxmoxIsoNaming.BuildDatastorePath(
                "iso/", WorkspaceId, "sub/foo. iso"));

        var normalized = ProxmoxIsoNaming.NormalizeFilename("foo._iso");
        Assert.Equal("foo._iso", normalized);
        Assert.Equal(normalized, ProxmoxIsoNaming.NormalizeFilename(normalized));

        Assert.Throws<HypervisorException>(
            () => ProxmoxIsoNaming.BuildDatastorePath("iso/", "not-a-guid", "f.iso"));

    }
    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
