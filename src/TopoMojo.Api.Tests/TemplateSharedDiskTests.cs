// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

// From #129: deleting a template must not delete a disk another template's detail still uses.
public class TemplateSharedDiskTests
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

    // The other half of the fix, for the clones that already exist: a template that still shares its
    // source's disk must not take that disk with it.
    [Fact]
    public void ExcludeSharedDisks_DropsADiskAnotherTemplateAlsoCarries()
    {
        string path = $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk";
        var deployable = TemplateOf(SourceDetail(path));

        string[] shared = TemplateService.ExcludeSharedDisks(deployable, [SourceDetail(path)]);

        Assert.Equal([path], shared);
        Assert.Empty(deployable.Disks);
    }

    [Fact]
    public void ExcludeSharedDisks_KeepsADiskNoOtherTemplateCarries()
    {
        string path = $"[ds] {WorkspaceId}/{CloneTemplateId}_0.vmdk";
        var deployable = TemplateOf(SourceDetail(path));

        string[] shared = TemplateService.ExcludeSharedDisks(
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
    public void ExcludeSharedDisks_FindsASharedDiskWhosePathIsEscapedInTheStoredDetail()
    {
        string path = $"[ds] {WorkspaceId}/bob's café & co_0.vmdk";
        string stored = new TemplateUtility(SourceDetail(path)).ToString();
        var deployable = TemplateOf(SourceDetail(path));

        Assert.DoesNotContain(path, stored);

        string[] shared = TemplateService.ExcludeSharedDisks(deployable, [stored]);

        Assert.Equal([path], shared);
        Assert.Empty(deployable.Disks);
    }

    // Detail is free text that nothing validates as json, so one unparseable row must not be able to
    // block every template delete.
    [Fact]
    public void ExcludeSharedDisks_IgnoresADetailThatIsNotJson()
    {
        string path = $"[ds] {WorkspaceId}/{SourceTemplateId}_0.vmdk";
        var deployable = TemplateOf(SourceDetail(path));

        string[] shared = TemplateService.ExcludeSharedDisks(deployable, ["not json at all"]);

        Assert.Empty(shared);
        Assert.Equal(path, deployable.Disks.Single().Path);
    }

    // An empty detail parses to a placeholder disk. That is not a claim on anything: a template with
    // no detail of its own resolves its disks through its parent.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExcludeSharedDisks_IgnoresADetailThatIsEmpty(string detail)
    {
        string path = $"[ds] {Guid.Empty}/placeholder.vmdk";
        var deployable = TemplateOf(SourceDetail(path));

        string[] shared = TemplateService.ExcludeSharedDisks(deployable, [detail]);

        Assert.Empty(shared);
        Assert.Equal(path, deployable.Disks.Single().Path);
    }

    [Fact]
    public void ExcludeSharedDisks_DropsOnlyTheSharedDiskOfSeveral()
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

        string[] shared = TemplateService.ExcludeSharedDisks(deployable, [SourceDetail(sharedPath)]);

        Assert.Equal([sharedPath], shared);
        Assert.Equal(ownPath, deployable.Disks.Single().Path);
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

}
