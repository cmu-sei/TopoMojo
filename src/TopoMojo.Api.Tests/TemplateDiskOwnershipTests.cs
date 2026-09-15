// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

public class TemplateDiskOwnershipTests
{
    private const string WorkspaceId = "5cd41cbe3f7b4bd0a1f6f4d1e2a3b4c5";
    private const string SourceTemplateId = "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d";
    private const string CloneTemplateId = "9f8e7d6c5b4a39281706f5e4d3c2b1a0";

    private static string SourceDetail(string diskPath) => $$"""
    {
      "name": "win10-base",
      "cpu": "1x2",
      "ram": 4,
      "adapters": 1,
      "eth": [ { "net": "lan", "type": "e1000" } ],
      "disks": [ { "path": "{{diskPath}}", "source": "", "controller": "lsilogic", "size": 40 } ]
    }
    """;

    // The bug this guards: a clone kept the source's disk path, so deleting the clone deleted the
    // disk the source still needed.
    [Fact]
    public void LocalizeCloneDiskPaths_GivesTheCloneAPathOfItsOwn()
    {
        string sourcePath = $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk";

        var disk = SingleDiskOf(
            TemplateService.LocalizeCloneDiskPaths(SourceDetail(sourcePath), WorkspaceId, CloneTemplateId)
        );

        Assert.Equal($"[ds] {WorkspaceId}/{CloneTemplateId}_0.vmdk", disk.Path);
        Assert.NotEqual(sourcePath, disk.Path);
    }

    // Without a Source the clone's disk is never created, so it would deploy against a path that
    // does not exist rather than a copy of the source's disk.
    [Fact]
    public void LocalizeCloneDiskPaths_RecordsTheSourcePathAsTheDiskSource()
    {
        string sourcePath = $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk";

        var disk = SingleDiskOf(
            TemplateService.LocalizeCloneDiskPaths(SourceDetail(sourcePath), WorkspaceId, CloneTemplateId)
        );

        Assert.Equal(sourcePath, disk.Source);
    }

    // A template with no workspace is stock, and its disks live in the public folder keyed by the
    // empty guid -- the same folder TemplateUtility generates a default disk path into.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LocalizeCloneDiskPaths_ForAStockTemplate_LocalizesIntoThePublicFolder(string workspaceId)
    {
        string sourcePath = $"[ds] {Guid.Empty}/win10-base.vmdk";

        var disk = SingleDiskOf(
            TemplateService.LocalizeCloneDiskPaths(SourceDetail(sourcePath), workspaceId, CloneTemplateId)
        );

        Assert.Equal($"[ds] {Guid.Empty}/{CloneTemplateId}_0.vmdk", disk.Path);
        Assert.Equal(sourcePath, disk.Source);
    }

    // Cloning a linked template carries no detail to localize. LocalizeDiskPaths rejects an empty
    // key, so this has to be handled before it is reached.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void LocalizeCloneDiskPaths_WithNoDetail_ReturnsItUnchanged(string detail)
    {
        Assert.Equal(detail, TemplateService.LocalizeCloneDiskPaths(detail, WorkspaceId, CloneTemplateId));
    }

    [Fact]
    public void LocalizeCloneDiskPaths_LocalizesEveryDiskByIndex()
    {
        string detail = $$"""
        {
          "name": "multi-disk",
          "eth": [ { "net": "lan", "type": "e1000" } ],
          "disks": [
            { "path": "[ds] {{WorkspaceId}}/{{SourceTemplateId}}_0.vmdk", "size": 40 },
            { "path": "[ds] {{WorkspaceId}}/{{SourceTemplateId}}_1.vmdk", "size": 10 }
          ]
        }
        """;

        var disks = DisksOf(
            TemplateService.LocalizeCloneDiskPaths(detail, WorkspaceId, CloneTemplateId)
        );

        Assert.Equal(2, disks.Length);
        Assert.Equal($"[ds] {WorkspaceId}/{CloneTemplateId}_0.vmdk", disks[0].Path);
        Assert.Equal($"[ds] {WorkspaceId}/{CloneTemplateId}_1.vmdk", disks[1].Path);
        Assert.Equal($"[ds] {WorkspaceId}/{SourceTemplateId}_1.vmdk", disks[1].Source);
    }

    // On Proxmox the shared artifact is the named template rather than a vmdk path, and deleting a
    // template there deletes it wholesale, so the clone needs a name of its own too.
    [Fact]
    public void LocalizeCloneDiskPaths_RepointsAProxmoxTemplateAtTheSourceAsItsParent()
    {
        string detail = $$"""
        {
          "name": "win10-base",
          "template": "win10-base-pve",
          "eth": [ { "net": "lan", "type": "e1000" } ],
          "disks": [ { "path": "[ds] {{WorkspaceId}}/{{SourceTemplateId}}_0.vmdk", "size": 40 } ]
        }
        """;

        var template = TemplateOf(
            TemplateService.LocalizeCloneDiskPaths(detail, WorkspaceId, CloneTemplateId)
        );

        Assert.Equal(CloneTemplateId, template.Template);
        Assert.Equal("win10-base-pve", template.ParentTemplate);
    }

    // The other half of the fix, for the clones that already exist: a template that still shares its
    // source's disk must not take that disk with it.
    [Fact]
    public void ExcludeSharedArtifacts_DropsADiskAnotherTemplateAlsoCarries()
    {
        string path = $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk";
        var deployable = TemplateOf(SourceDetail(path));

        string[] shared = TemplateService.ExcludeSharedArtifacts(deployable, [SourceDetail(path)]);

        Assert.Equal([path], shared);
        Assert.Empty(deployable.Disks);
    }

    [Fact]
    public void ExcludeSharedArtifacts_KeepsADiskNoOtherTemplateCarries()
    {
        string path = $"[ds] {WorkspaceId}/{CloneTemplateId}_0.vmdk";
        var deployable = TemplateOf(SourceDetail(path));

        string[] shared = TemplateService.ExcludeSharedArtifacts(
            deployable,
            [SourceDetail($"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk")]
        );

        Assert.Empty(shared);
        Assert.Equal(path, deployable.Disks.Single().Path);
    }

    // Why the details are read whole instead of narrowed by a database query on the path: the stored
    // json escapes an apostrophe and anything non-ascii, so the column does not literally contain the
    // path, and a substring predicate would clear a disk that is still in use for deletion.
    [Fact]
    public void ExcludeSharedArtifacts_FindsASharedDiskWhosePathIsEscapedInTheStoredDetail()
    {
        string path = $"[ds] {WorkspaceId}/bob's café & co_0.vmdk";
        string stored = new TemplateUtility(SourceDetail(path)).ToString();
        var deployable = TemplateOf(SourceDetail(path));

        Assert.DoesNotContain(path, stored);

        string[] shared = TemplateService.ExcludeSharedArtifacts(deployable, [stored]);

        Assert.Equal([path], shared);
        Assert.Empty(deployable.Disks);
    }

    // Detail is free text that nothing validates as json, so one unparseable row must not be able to
    // block every template delete.
    [Fact]
    public void ExcludeSharedArtifacts_IgnoresADetailThatIsNotJson()
    {
        string path = $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk";
        var deployable = TemplateOf(SourceDetail(path));

        string[] shared = TemplateService.ExcludeSharedArtifacts(deployable, ["not json at all"]);

        Assert.Empty(shared);
        Assert.Equal(path, deployable.Disks.Single().Path);
    }

    // An empty detail parses to a placeholder disk. That is not a claim on anything: a template with
    // no detail of its own resolves its disks through its parent.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExcludeSharedArtifacts_IgnoresADetailThatIsEmpty(string detail)
    {
        string path = $"[ds] {Guid.Empty}/placeholder.vmdk";
        var deployable = TemplateOf(SourceDetail(path));

        string[] shared = TemplateService.ExcludeSharedArtifacts(deployable, [detail]);

        Assert.Empty(shared);
        Assert.Equal(path, deployable.Disks.Single().Path);
    }

    [Fact]
    public void ExcludeSharedArtifacts_DropsOnlyTheSharedDiskOfSeveral()
    {
        string sharedPath = $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk";
        string ownPath = $"[ds] {WorkspaceId}/{CloneTemplateId}_1.vmdk";

        var deployable = TemplateOf($$"""
        {
          "name": "multi-disk",
          "disks": [
            { "path": "{{sharedPath}}", "size": 40 },
            { "path": "{{ownPath}}", "size": 10 }
          ]
        }
        """);

        string[] shared = TemplateService.ExcludeSharedArtifacts(deployable, [SourceDetail(sharedPath)]);

        Assert.Equal([sharedPath], shared);
        Assert.Equal(ownPath, deployable.Disks.Single().Path);
    }

    // On Proxmox the disks belong to a named template, and deleting that name deletes them, so a
    // shared name has to be withheld from the hypervisor the way a shared disk path is.
    [Fact]
    public void ExcludeSharedArtifacts_DropsASharedProxmoxTemplateName()
    {
        string detail = $$"""
        { "name": "win10-base", "template": "win10-base-pve", "disks": [] }
        """;

        var deployable = TemplateOf(detail);

        string[] shared = TemplateService.ExcludeSharedArtifacts(deployable, [detail]);

        Assert.Equal(["win10-base-pve"], shared);
        Assert.Null(deployable.Template);
    }

    [Fact]
    public void ExcludeSharedArtifacts_KeepsAProxmoxTemplateNameOfItsOwn()
    {
        var deployable = TemplateOf($$"""
        { "name": "win10-base", "template": "{{CloneTemplateId}}", "disks": [] }
        """);

        string[] shared = TemplateService.ExcludeSharedArtifacts(
            deployable,
            ["""{ "name": "win10-base", "template": "win10-base-pve", "disks": [] }"""]
        );

        Assert.Empty(shared);
        Assert.Equal(CloneTemplateId, deployable.Template);
    }

    // Paths reach this comparison from stored detail, where the datastore prefix is a convention
    // rather than a guarantee, so it cannot be what decides whether a disk is shared.
    [Fact]
    public void SameDiskFile_IgnoresTheDatastorePrefix()
    {
        Assert.True(TemplateService.SameDiskFile(
            $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk",
            $"{WorkspaceId}/{SourceTemplateId}_0.vmdk"
        ));
    }

    [Fact]
    public void SameDiskFile_IsFalseForADifferentFolderOrFile()
    {
        string path = $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk";

        Assert.False(TemplateService.SameDiskFile(path, $"[ds] {Guid.Empty}/{SourceTemplateId}_0.vmdk"));
        Assert.False(TemplateService.SameDiskFile(path, $"[ds] {WorkspaceId}/{SourceTemplateId}_1.vmdk"));
        Assert.False(TemplateService.SameDiskFile(path, path + ".bak"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SameDiskFile_IsFalseWithoutAPath(string path)
    {
        Assert.False(TemplateService.SameDiskFile(path, $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk"));
        Assert.False(TemplateService.SameDiskFile($"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk", path));
    }

    private static VmTemplate TemplateOf(string detail) => new TemplateUtility(detail).AsTemplate();

    private static VmDisk[] DisksOf(string detail) => TemplateOf(detail).Disks;

    private static VmDisk SingleDiskOf(string detail) => DisksOf(detail).Single();
}
