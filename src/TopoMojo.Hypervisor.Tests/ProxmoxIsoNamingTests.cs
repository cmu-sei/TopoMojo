using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class ProxmoxIsoNamingTests
{
    private const string WorkspaceId = "0123456789abcdef0123456789abcdef";
    private const string PublicId = "00000000-0000-0000-0000-000000000000";

    [Fact]
    public void NormalizeFilename_MirrorsProxmoxSafeNaming()
    {
        var inputs = new[]
        {
            (Input: "My File(1).iso", Expected: "My_File_1_.iso"),
            (Input: "a##b.iso", Expected: "a_b.iso"),
            (Input: "../../etc/passwd.iso", Expected: "passwd.iso")
        };

        foreach (var (input, expected) in inputs)
        {
            var normalized = ProxmoxIsoNaming.NormalizeFilename(input);
            Assert.Equal(expected, normalized);
            Assert.Equal(normalized, ProxmoxIsoNaming.NormalizeFilename(normalized));
        }
    }

    [Fact]
    public void Encode_UsesPveSafeScopeSeparator()
    {
        Assert.Equal(
            $"{WorkspaceId}__My_File.iso",
            ProxmoxIsoNaming.Encode(WorkspaceId, "My File.iso"));
    }

    [Fact]
    public void TryDecode_RecoversNewAndLegacyNames()
    {
        Assert.True(
            ProxmoxIsoNaming.TryDecode(
                $"{WorkspaceId}__My_File.iso",
                out var scopeId,
                out var fileName));
        Assert.Equal(WorkspaceId, scopeId);
        Assert.Equal("My_File.iso", fileName);

        Assert.True(
            ProxmoxIsoNaming.TryDecode(
                $"{WorkspaceId}#My File.iso",
                out scopeId,
                out fileName));
        Assert.Equal(WorkspaceId, scopeId);
        Assert.Equal("My File.iso", fileName);
    }

    [Fact]
    public void TryDecode_RejectsUnscopedNames()
    {
        Assert.True(ProxmoxIsoNaming.TryDecode($"{PublicId}__x.iso", out var scopeId, out var fileName));
        Assert.Equal(PublicId, scopeId);
        Assert.Equal("x.iso", fileName);
        Assert.False(ProxmoxIsoNaming.TryDecode("ubuntu__24.iso", out _, out _));
        Assert.False(ProxmoxIsoNaming.TryDecode("ubuntu.iso", out _, out _));
    }

    [Fact]
    public void TrySplitDatastorePath_RequiresStorageScopeAndFile()
    {
        Assert.True(
            ProxmoxIsoNaming.TrySplitDatastorePath(
                $"iso/{WorkspaceId}/f.iso",
                out var storage,
                out var scopeId,
                out var fileName));
        Assert.Equal("iso", storage);
        Assert.Equal(WorkspaceId, scopeId);
        Assert.Equal("f.iso", fileName);

        Assert.False(ProxmoxIsoNaming.TrySplitDatastorePath("iso/f.iso", out _, out _, out _));
        Assert.False(ProxmoxIsoNaming.TrySplitDatastorePath("iso/a/b/c.iso", out _, out _, out _));
    }

    [Fact]
    public void StorageName_TrimsPathSeparators()
    {
        Assert.Equal("iso", ProxmoxIsoNaming.StorageName("iso/"));
    }
}
