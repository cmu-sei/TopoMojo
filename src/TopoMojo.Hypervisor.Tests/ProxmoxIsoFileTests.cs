using TopoMojo.Hypervisor.Proxmox;
using TopoMojo.Hypervisor.Proxmox.Models;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class ProxmoxIsoFileTests
{
    private const string WorkspaceId = "0123456789abcdef0123456789abcdef";
    private const string PublicId = "00000000-0000-0000-0000-000000000000";
    private const string VolidPrefix = "iso:iso/";

    [Theory]
    [InlineData("__", PublicId + "__x.iso", PublicId + "/x.iso", PublicId, "x.iso")]
    [InlineData("__", WorkspaceId + "#My File.iso", WorkspaceId + "/My File.iso", WorkspaceId, "My File.iso")]
    [InlineData("__", WorkspaceId + "#9b3b331c-10c1-448b-8114-21b2586d8e38#file.iso",
        WorkspaceId + "/9b3b331c-10c1-448b-8114-21b2586d8e38#file.iso",
        WorkspaceId, "9b3b331c-10c1-448b-8114-21b2586d8e38#file.iso")]
    [InlineData("__", "ubuntu-24.04.iso", "ubuntu-24.04.iso", null, "ubuntu-24.04.iso")]
    [InlineData("-", WorkspaceId + "-x.iso", WorkspaceId + "/x.iso", WorkspaceId, "x.iso")]
    public void From_DecodesVolidIntoNameScopeAndDisplayName(
        string separator,
        string storedName,
        string expectedDisplayName,
        string expectedScopeId,
        string expectedScopedFileName)
    {
        var iso = ProxmoxIsoFile.From(new PveIso { Volid = VolidPrefix + storedName }, separator);

        Assert.Equal(storedName, iso.Name);
        Assert.Equal(expectedDisplayName, iso.DisplayName);
        Assert.Equal(expectedScopeId, iso.ScopeId);
        Assert.Equal(expectedScopedFileName, iso.ScopedFileName);
    }

    [Fact]
    public void MatchIso_MatchesDecodedScopedDisplayName()
    {
        const string scopeId = "44444444-4444-4444-4444-444444444444";
        var current = ProxmoxIsoFile.From(new PveIso { Volid = $"iso:iso/{scopeId}__x.iso" }, "__");
        var legacy = ProxmoxIsoFile.From(new PveIso { Volid = $"iso:iso/{scopeId}#y.iso" }, "__");

        Assert.Equal(current.Volid, ProxmoxClient.MatchIso([current, legacy], $"{scopeId}/x.iso")?.Volid);
        Assert.Equal(legacy.Volid, ProxmoxClient.MatchIso([current, legacy], $"{scopeId}/y.iso")?.Volid);
    }

    [Fact]
    public void MatchIso_DoesNotFabricateScopeFromAnUnscopedHashName()
    {
        var unscoped = ProxmoxIsoFile.From(new PveIso { Volid = "iso:iso/ubuntu#24.iso" }, "__");

        Assert.Equal(unscoped.Volid, ProxmoxClient.MatchIso([unscoped], "ubuntu#24.iso")?.Volid);
        Assert.Null(ProxmoxClient.MatchIso([unscoped], "ubuntu/24.iso"));
    }
}
