using System;
using TopoMojo.Hypervisor.Proxmox;
using TopoMojo.Hypervisor.Exceptions;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class ProxmoxIsoNamingTests
{
    private const string WorkspaceId = "0123456789abcdef0123456789abcdef";
    private const string PublicId = "00000000-0000-0000-0000-000000000000";
    private const string Sep = ProxmoxIsoNaming.DefaultScopeSeparator;

    [Theory]
    [InlineData("My File(1).iso", "My_File_1_.iso")]
    [InlineData("a##b.iso", "a_b.iso")]
    [InlineData("../../etc/passwd.iso", "passwd.iso")]
    [InlineData("foo._iso", "foo._iso")]
    public void NormalizeFilename_MirrorsProxmoxSafeNaming(string input, string expected)
    {
        var normalized = ProxmoxIsoNaming.NormalizeFilename(input);
        Assert.Equal(expected, normalized);
        Assert.Equal(normalized, ProxmoxIsoNaming.NormalizeFilename(normalized));
    }

    [Theory]
    [InlineData("__", WorkspaceId, "My File.iso", WorkspaceId + "__My_File.iso", "My_File.iso")]
    [InlineData("__", PublicId, "x.iso", PublicId + "__x.iso", "x.iso")]
    [InlineData("_x_", WorkspaceId, "My File.iso", WorkspaceId + "_x_My_File.iso", "My_File.iso")]
    [InlineData("_x_", PublicId, "My File.iso", PublicId + "_x_My_File.iso", "My_File.iso")]
    [InlineData("-", WorkspaceId, "My File.iso", WorkspaceId + "-My_File.iso", "My_File.iso")]
    // A dashed guid scope still decodes: the scan only accepts a prefix that parses as a Guid.
    [InlineData("-", PublicId, "x.iso", PublicId + "-x.iso", "x.iso")]
    public void EncodeAndTryDecode_RoundTripAcrossSeparators(
        string separator,
        string scopeId,
        string fileName,
        string expectedStoredName,
        string expectedDecodedFileName)
    {
        var stored = ProxmoxIsoNaming.Encode(scopeId, fileName, separator);
        Assert.Equal(expectedStoredName, stored);

        Assert.True(ProxmoxIsoNaming.TryDecode(stored, separator, out var decodedScopeId, out var decodedFileName));
        Assert.Equal(scopeId, decodedScopeId);
        Assert.Equal(expectedDecodedFileName, decodedFileName);
    }

    [Theory]
    [InlineData("__", "ubuntu__24.iso")]
    [InlineData("__", "ubuntu.iso")]
    [InlineData("_x_", WorkspaceId + "__x.iso")]
    [InlineData("-", "ubuntu-24.04.iso")]
    public void TryDecode_RejectsNamesWithoutADecodableScope(string separator, string storedName)
        => Assert.False(ProxmoxIsoNaming.TryDecode(storedName, separator, out _, out _));

    [Fact]
    public void TryDecode_AcceptsTheLegacyHashSeparator()
    {
        Assert.True(ProxmoxIsoNaming.TryDecode($"{WorkspaceId}#My File.iso", Sep, out var scopeId, out var fileName));
        Assert.Equal(WorkspaceId, scopeId);
        // The legacy branch returns the stored basename verbatim; it is not re-normalized.
        Assert.Equal("My File.iso", fileName);
    }

    [Fact]
    public void EncodeLegacy_ReproducesThePreConfigurableFlatName()
    {
        var legacy = ProxmoxIsoNaming.EncodeLegacy(WorkspaceId, "sub/My File.iso");

        Assert.Equal($"{WorkspaceId}#MyFile.iso", legacy);
        Assert.NotEqual(ProxmoxIsoNaming.Encode(WorkspaceId, "sub/My File.iso", Sep), legacy);

        Assert.True(ProxmoxIsoNaming.TryDecode(legacy, Sep, out var scopeId, out var fileName));
        Assert.Equal(WorkspaceId, scopeId);
        Assert.Equal("MyFile.iso", fileName);

        Assert.Throws<ArgumentException>(() => ProxmoxIsoNaming.EncodeLegacy("not a scope", "x.iso"));
        Assert.Throws<ArgumentException>(() => ProxmoxIsoNaming.EncodeLegacy(WorkspaceId, "sub/ "));
    }

    [Fact]
    public void ValidateScopeSeparator_RejectsEmptyAndNonPveSafeValues()
    {
        Assert.Throws<HypervisorException>(() => ProxmoxIsoNaming.ValidateScopeSeparator(null));
        Assert.Throws<HypervisorException>(() => ProxmoxIsoNaming.ValidateScopeSeparator(""));
        Assert.Throws<HypervisorException>(() => ProxmoxIsoNaming.ValidateScopeSeparator(" "));
        Assert.Throws<HypervisorException>(() => ProxmoxIsoNaming.ValidateScopeSeparator("#"));

        ProxmoxIsoNaming.ValidateScopeSeparator("__");
        ProxmoxIsoNaming.ValidateScopeSeparator("_x_");
        ProxmoxIsoNaming.ValidateScopeSeparator("-");
        ProxmoxIsoNaming.ValidateScopeSeparator(".");
    }

    [Theory]
    [InlineData("iso/", "iso")]
    [InlineData("iso", "iso")]
    [InlineData("/iso/", "iso")]
    [InlineData(null, "")]
    public void StorageName_TrimsPathSeparators(string isoStore, string expected)
    {
        Assert.Equal(expected, ProxmoxIsoNaming.StorageName(isoStore));
    }

    [Fact]
    public void BuildDatastorePath_NormalizesBasenameAndRequiresAGuidScope()
    {
        Assert.Equal(
            $"iso/{WorkspaceId}/foo._iso",
            ProxmoxIsoNaming.BuildDatastorePath("iso/", WorkspaceId, "sub/foo. iso"));

        Assert.Throws<HypervisorException>(
            () => ProxmoxIsoNaming.BuildDatastorePath("iso/", "not-a-guid", "f.iso"));
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
}
