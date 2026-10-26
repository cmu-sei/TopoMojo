using System;
using System.Linq;
using TopoMojo.Hypervisor;
using TopoMojo.Hypervisor.vSphere;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class VSphereDiskOptionsTests
{
    private static readonly DateTimeOffset Modified = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToTemplateSources_ListsDescriptorsAsTemplatePaths()
    {
        var options = new HypervisorServiceConfiguration
        {
            DiskStore = "[topomojo]",
            VmStore = "[topomojo] _run/"
        };

        DatastoreFile[] files =
        [
            new("[topomojo]/00000000-0000-0000-0000-000000000000/ubuntu.vmdk", Modified),
            new("[topomojo]/00000000-0000-0000-0000-000000000000/ubuntu-flat.vmdk", Modified),
            new("[topomojo] stock/kali.vmdk", null),
            new("[topomojo] stock/kali-000001.vmdk", null),
            new("[topomojo] stock/kali-ctk.vmdk", null),
            new("[topomojo] _run/vm1#abc/vm1.vmdk", null),
            new("[other] stock/elsewhere.vmdk", null)
        ];

        var sources = VSphereHypervisorService.ToTemplateSources(files, options);

        Assert.Equal(
            new[]
            {
                "[ds] 00000000-0000-0000-0000-000000000000/ubuntu.vmdk",
                "[ds] stock/kali.vmdk"
            },
            sources.Select(s => s.Name));
        Assert.Equal("00000000-0000-0000-0000-000000000000", sources[0].WorkspaceId);
        Assert.Equal(Modified, sources[0].Modified);
        Assert.Equal("stock", sources[1].WorkspaceId);
        Assert.Null(sources[1].Modified);
    }

    [Fact]
    public void ToTemplateSources_NamesTheTemplateOfALocalizedDisk()
    {
        var options = new HypervisorServiceConfiguration { DiskStore = "[topomojo]" };

        DatastoreFile[] files =
        [
            new("[topomojo] aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb_0.vmdk", null),
            new("[topomojo] root-disk.vmdk", null)
        ];

        var sources = VSphereHypervisorService.ToTemplateSources(files, options);

        Assert.Equal("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", sources[0].WorkspaceId);
        Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", sources[0].TemplateId);
        Assert.Equal("[ds] root-disk.vmdk", sources[1].Name);
        Assert.Null(sources[1].WorkspaceId);
        Assert.Null(sources[1].TemplateId);
    }

    [Fact]
    public void ToTemplateSources_IsRelativeToADiskStoreFolder()
    {
        var options = new HypervisorServiceConfiguration
        {
            DiskStore = "[san] disks/",
            VmStore = "[san] _run/"
        };

        DatastoreFile[] files =
        [
            new("[san] disks/stock/win10.vmdk", null),
            new("[san] other/notadisk.vmdk", null)
        ];

        Assert.Equal(
            new[] { "[ds] stock/win10.vmdk" },
            VSphereHypervisorService.ToTemplateSources(files, options).Select(s => s.Name));
    }
}
