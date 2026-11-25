// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

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

    [Fact]
    public void ToTemplateSources_DropsVmFoldersWhenTheVmStoreIsTheDiskStore()
    {
        var options = new HypervisorServiceConfiguration
        {
            DiskStore = "[datastore3] topo-dev/",
            VmStore = "[datastore3] topo-dev/"
        };

        DatastoreFile[] files =
        [
            new("[datastore3] topo-dev/00000000-0000-0000-0000-000000000000/blank-50g.vmdk", null),
            new("[datastore3] topo-dev/blank-50g-clone#00000000-0000-0000-0000-000000000000/blank-50g.vmdk", null)
        ];

        Assert.Equal(
            new[] { "[ds] 00000000-0000-0000-0000-000000000000/blank-50g.vmdk" },
            VSphereHypervisorService.ToTemplateSources(files, options).Select(s => s.Name));
    }

    [Theory]
    [InlineData("[topomojo]")]
    [InlineData("[topomojo] disks/")]
    public void ToTemplateSources_DropsVmFoldersWhenTheVmStoreIsAboveTheDiskStore(string diskStore)
    {
        var options = new HypervisorServiceConfiguration { DiskStore = diskStore, VmStore = "[topomojo]" };
        string root = diskStore == "[topomojo]" ? "[topomojo] " : "[topomojo] disks/";

        DatastoreFile[] files =
        [
            new(root + "stock/kali.vmdk", null),
            new(root + "kali#abc/kali.vmdk", null)
        ];

        Assert.Equal(
            new[] { "[ds] stock/kali.vmdk" },
            VSphereHypervisorService.ToTemplateSources(files, options).Select(s => s.Name));
    }

    [Fact]
    public void ToTemplateSources_MatchesAnyHostInTheVmStoreMacro()
    {
        var options = new HypervisorServiceConfiguration
        {
            DiskStore = "[topomojo]",
            VmStore = "[topomojo] _run-{host}/"
        };

        DatastoreFile[] files =
        [
            new("[topomojo] stock/kali.vmdk", null),
            // a vm folder without a '#', so only the VmStore match drops it
            new("[topomojo] _run-esx1/leftover/leftover.vmdk", null)
        ];

        Assert.Equal(
            new[] { "[ds] stock/kali.vmdk" },
            VSphereHypervisorService.ToTemplateSources(files, options).Select(s => s.Name));
    }

    [Fact]
    public void ToTemplateSources_IgnoresSlashesAroundTheDiskStoreFolder()
    {
        var options = new HypervisorServiceConfiguration { DiskStore = "[san] /disks/", VmStore = "[san] /_run/" };

        DatastoreFile[] files = [new("[san] disks/stock/win10.vmdk", null)];

        Assert.Equal(
            new[] { "[ds] stock/win10.vmdk" },
            VSphereHypervisorService.ToTemplateSources(files, options).Select(s => s.Name));
    }

    [Fact]
    public void ToTemplateSources_KeepsDisksNamedForADate()
    {
        var options = new HypervisorServiceConfiguration { DiskStore = "[topomojo]" };

        DatastoreFile[] files =
        [
            new("[topomojo] stock/win10-240115.vmdk", null),
            new("[topomojo] stock/win10-000002.vmdk", null)
        ];

        Assert.Equal(
            new[] { "[ds] stock/win10-240115.vmdk" },
            VSphereHypervisorService.ToTemplateSources(files, options).Select(s => s.Name));
    }

    [Fact]
    public void ToTemplateSources_NamesTheTemplateOfADiskWithADashedId()
    {
        var options = new HypervisorServiceConfiguration { DiskStore = "[topomojo]" };

        DatastoreFile[] files =
        [
            new("[topomojo] 67dade91-dd4c-4343-bf19-6bc7c7f9e48c/656b76dc-a894-4594-a043-daab251c46ad_0.vmdk", null)
        ];

        var source = VSphereHypervisorService.ToTemplateSources(files, options).Single();

        Assert.Equal("67dade91-dd4c-4343-bf19-6bc7c7f9e48c", source.WorkspaceId);
        Assert.Equal("656b76dc-a894-4594-a043-daab251c46ad", source.TemplateId);
    }

    [Fact]
    public void ToTemplateSources_NamesTheTemplateOfADiskInATenantWorkspace()
    {
        // Core:Tenant prefixes workspace ids; template ids stay plain hex
        var options = new HypervisorServiceConfiguration { DiskStore = "[topomojo]" };

        DatastoreFile[] files =
        [
            new("[topomojo] zztopoqw4b1c9a7e2f3d4c5b6a7f8e9d/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb_0.vmdk", null)
        ];

        var source = VSphereHypervisorService.ToTemplateSources(files, options).Single();

        Assert.Equal("zztopoqw4b1c9a7e2f3d4c5b6a7f8e9d", source.WorkspaceId);
        Assert.Equal("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", source.TemplateId);
    }
}
