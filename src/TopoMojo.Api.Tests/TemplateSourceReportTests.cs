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

    [Theory]
    [InlineData("", "qq")]
    [InlineData("zz", "")]
    [InlineData("zz", "qq")]
    public void AnotherTenantsWorkspacesAreLeftOut(string tenant, string other)
    {
        string mine = string.Concat(tenant, Guid.NewGuid().ToString("n").AsSpan(tenant.Length));
        string theirs = string.Concat(other, Guid.NewGuid().ToString("n").AsSpan(other.Length));

        var report = TemplateSourceReport.Build(
            new TemplateSourceOptions
            {
                Field = "disks",
                Sources = [
                    new TemplateSource { Name = "mine", WorkspaceId = mine },
                    new TemplateSource { Name = "theirs", WorkspaceId = theirs, TemplateId = TemplateId },
                    new TemplateSource { Name = "global", WorkspaceId = Guid.Empty.ToString() }
                ]
            },
            new Dictionary<string, TemplateSourceReport.TemplateRef>(),
            Workspaces,
            tenant);

        Assert.Equal(new[] { "mine", "global" }, report.Sources.Select(s => s.Name));
        Assert.Equal("(deleted)", report.Sources[0].WorkspaceName);
    }

    [Theory]
    [InlineData("4b1c9a7e2f3d4c5b6a7f8e9d0a1b2c3d", "")]
    [InlineData("4b1c9a7e-2f3d-4c5b-6a7f-8e9d0a1b2c3d", "")]
    [InlineData("zztopoqw4b1c9a7e2f3d4c5b6a7f8e9d", "zztopoqw")]
    [InlineData("ZZ4b1c9a7e2f3d4c5b6a7f8e9d0a1b2c", "zz")]
    public void TheTenantIsTheLeadingNonHexLetters(string id, string tenant)
    {
        Assert.Equal(tenant, TemplateSourceReport.TenantOf(id));
    }

    [Fact]
    public void AnotherTenantsDiskStaysOutWhenItsTemplateIsKnownHere()
    {
        // the same export imported into both tenants keeps its template ids
        string theirs = string.Concat("qq", Guid.NewGuid().ToString("n").AsSpan(2));

        var report = TemplateSourceReport.Build(
            new TemplateSourceOptions
            {
                Field = "disks",
                Sources = [new TemplateSource { Name = "theirs", WorkspaceId = theirs, TemplateId = TemplateId }]
            },
            Templates,
            Workspaces,
            "zz");

        Assert.Empty(report.Sources);
    }

    [Theory]
    [InlineData("zz")]
    [InlineData("zztopoqw")]
    [InlineData("zztopoqwgg")]
    public void AnImportedDashedIdWithATenantPrefixIsAWorkspace(string tenant)
    {
        // TransferService.UpdateTenant writes the tenant over the start of a dashed id
        string legacy = "67dade91-dd4c-4343-bf19-6bc7c7f9e48c";
        string id = string.Concat(tenant, legacy.AsSpan(tenant.Length));
        var workspaces = new Dictionary<string, string> { [id] = "Imported Lab" };

        var entry = TemplateSourceReport.Build(
            new TemplateSourceOptions { Field = "disks", Sources = [new TemplateSource { Name = "[ds] x/d.vmdk", WorkspaceId = id }] },
            Templates,
            workspaces,
            tenant).Sources.Single();

        Assert.Equal(id, TemplateSourceReport.NormalizeId(id));
        Assert.Equal("Imported Lab", entry.WorkspaceName);
    }

    [Fact]
    public void ADashedIdReimportedUnderAShorterTenantIsAWorkspace()
    {
        // TransferService.UpdateTenant: legacy id under "zztopoqwgg", then again under "zz",
        // which pads the old tenant's length with zeros over where the first dash was
        string id = "zz00000000d4c-4343-bf19-6bc7c7f9e48c";

        Assert.Equal(id, TemplateSourceReport.NormalizeId(id));
        Assert.Equal("zz", TemplateSourceReport.TenantOf(id));
    }
}
