using TopoMojo.Hypervisor.Proxmox;
using TopoMojo.Hypervisor.Proxmox.Models;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class ProxmoxIsoFileTests
{
    [Fact]
    public void PublicIso_UsesDecodedDisplayName()
    {
        var iso = ProxmoxIsoFile.From(
            new PveIso
            {
                Volid = "iso:iso/00000000-0000-0000-0000-000000000000__x.iso"
            },
            ProxmoxIsoNaming.DefaultScopeSeparator);

        Assert.Equal("00000000-0000-0000-0000-000000000000/x.iso", iso.DisplayName);
        Assert.Equal("00000000-0000-0000-0000-000000000000", iso.ScopeId);
    }

    [Fact]
    public void LegacyIso_UsesDecodedDisplayName()
    {
        var iso = ProxmoxIsoFile.From(
            new PveIso
            {
                Volid = "iso:iso/0123456789abcdef0123456789abcdef#My File.iso"
            },
            ProxmoxIsoNaming.DefaultScopeSeparator);

        Assert.Equal("0123456789abcdef0123456789abcdef/My File.iso", iso.DisplayName);
        Assert.Equal("0123456789abcdef0123456789abcdef", iso.ScopeId);
    }

    [Fact]
    public void UnscopedIso_RetainsStoredName()
    {
        var iso = ProxmoxIsoFile.From(
            new PveIso
            {
                Volid = "iso:iso/ubuntu-24.04.iso"
            },
            ProxmoxIsoNaming.DefaultScopeSeparator);

        Assert.Equal("ubuntu-24.04.iso", iso.DisplayName);
        Assert.Null(iso.ScopeId);
        Assert.Equal("ubuntu-24.04.iso", iso.ScopedFileName);
    }

    [Fact]
    public void Iso_DecodesWithANonDefaultSeparator()
    {
        var iso = ProxmoxIsoFile.From(
            new PveIso
            {
                Volid = "iso:iso/0123456789abcdef0123456789abcdef-x.iso"
            },
            "-");

        Assert.Equal("0123456789abcdef0123456789abcdef", iso.ScopeId);
        Assert.Equal("x.iso", iso.ScopedFileName);
    }
}
