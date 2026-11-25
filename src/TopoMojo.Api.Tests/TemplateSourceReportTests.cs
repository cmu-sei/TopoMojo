// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using TopoMojo.Api.Controllers;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class TemplateSourceReportTests
{
    private const string WorkspaceId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TemplateId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static readonly Dictionary<string, TemplateSourceReport.TemplateRef> Templates = new()
    {
        [TemplateId] = new("Kali Attacker", WorkspaceId)
    };

    private static readonly Dictionary<string, string> Workspaces = new()
    {
        [WorkspaceId] = "Red Team Lab"
    };

    private static TemplateSourceReport.Entry BuildOne(TemplateSource source)
        => TemplateSourceReport.Build(
            new TemplateSourceOptions { Field = "disks", Sources = [source] },
            Templates,
            Workspaces).Sources.Single();

    [Theory]
    [InlineData(null)]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("stock")]
    public void SourcesNamingNoWorkspaceAreGlobal(string workspaceId)
    {
        var entry = BuildOne(new TemplateSource { Name = "[ds] x/disk.vmdk", WorkspaceId = workspaceId });

        Assert.True(entry.IsGlobal);
        Assert.Null(entry.WorkspaceName);
    }

    [Fact]
    public void AWorkspaceFolderNamesItsWorkspace()
    {
        var entry = BuildOne(new TemplateSource { Name = "[ds] a/disk.vmdk", WorkspaceId = WorkspaceId });

        Assert.False(entry.IsGlobal);
        Assert.Equal("Red Team Lab", entry.WorkspaceName);
    }

    [Fact]
    public void ATemplateCopyNamesItsTemplateAndWorkspace()
    {
        // Proxmox copies carry only a template id
        var entry = BuildOne(new TemplateSource { Name = TemplateId, TemplateId = TemplateId });

        Assert.False(entry.IsGlobal);
        Assert.Equal(WorkspaceId, entry.WorkspaceId);
        Assert.Equal("Red Team Lab", entry.WorkspaceName);
        Assert.Equal("Kali Attacker", entry.TemplateName);
    }

    [Fact]
    public void MissingOwnersAreMarkedDeleted()
    {
        var gone = Guid.NewGuid().ToString("n");

        Assert.Equal("(deleted)", BuildOne(new TemplateSource { Name = gone, TemplateId = gone }).WorkspaceName);
        Assert.Equal("(deleted)", BuildOne(new TemplateSource { Name = "[ds] x/d.vmdk", WorkspaceId = gone }).WorkspaceName);
    }

    [Fact]
    public void ADiskWhoseTemplateIsGoneKeepsItsWorkspace()
    {
        var gone = Guid.NewGuid().ToString("n");

        var entry = BuildOne(new TemplateSource { Name = "[ds] a/t_0.vmdk", WorkspaceId = WorkspaceId, TemplateId = gone });

        Assert.Equal("Red Team Lab", entry.WorkspaceName);
        Assert.Equal("(deleted)", entry.TemplateName);
    }

    [Fact]
    public void ACopyWhoseTemplateIsGoneIsMarkedDeleted()
    {
        var gone = Guid.NewGuid().ToString("n");

        var entry = BuildOne(new TemplateSource { Name = gone, TemplateId = gone });

        Assert.Equal("(deleted)", entry.WorkspaceName);
        Assert.Equal("(deleted)", entry.TemplateName);
    }

    [Fact]
    public void DashedIdsResolveLikeHexIds()
    {
        var entry = BuildOne(new TemplateSource
        {
            Name = "[ds] a/t_0.vmdk",
            WorkspaceId = Guid.Parse(WorkspaceId).ToString("d"),
            TemplateId = Guid.Parse(TemplateId).ToString("d")
        });

        Assert.Equal("Red Team Lab", entry.WorkspaceName);
        Assert.Equal("Kali Attacker", entry.TemplateName);
    }

    [Fact]
    public void LookupsCoverBothStoredIdForms()
    {
        Assert.Equal(
            new[] { WorkspaceId, Guid.Parse(WorkspaceId).ToString("d") },
            TemplateSourceReport.StoredForms([WorkspaceId, WorkspaceId]));
    }

    [Theory]
    [InlineData("zz")]
    [InlineData("zztopoqw")]
    public void TenantWorkspaceIdsResolveLikeHexIds(string tenant)
    {
        // as WorkspaceService makes them with Core:Tenant set
        string id = string.Concat(tenant, Guid.NewGuid().ToString("n").AsSpan(tenant.Length));
        var workspaces = new Dictionary<string, string> { [id] = "Tenant Lab" };

        var entry = TemplateSourceReport.Build(
            new TemplateSourceOptions { Field = "disks", Sources = [new TemplateSource { Name = "[ds] x/d.vmdk", WorkspaceId = id }] },
            Templates,
            workspaces).Sources.Single();

        Assert.False(entry.IsGlobal);
        Assert.Equal("Tenant Lab", entry.WorkspaceName);
        Assert.Equal(new[] { id }, TemplateSourceReport.StoredForms([TemplateSourceReport.NormalizeId(id)]));
    }

    [Theory]
    [InlineData("stock")]
    [InlineData("zz-not-an-id")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void FoldersThatArentIdsStayGlobal(string folder)
    {
        Assert.Null(TemplateSourceReport.NormalizeId(folder));
        Assert.True(BuildOne(new TemplateSource { Name = $"[ds] {folder}/d.vmdk", WorkspaceId = folder }).IsGlobal);
    }
}
