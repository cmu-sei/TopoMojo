// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Controllers;
using TopoMojo.Api.Data;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

/// <summary>
/// api/admin/template-sources looks each source's owner up in the database. Ids are stored in
/// every form TopoMojo has made them: 32 hex digits, dashed (older versions and imports), and
/// with a Core:Tenant prefix. Each form must find its owner.
/// </summary>
public sealed class TemplateSourceLookupTests : IDisposable
{
    private readonly TopoMojoDbContextInMemory _db = new(
        new DbContextOptionsBuilder<TopoMojoDbContextInMemory>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options
    );

    public void Dispose() => _db.Dispose();

    // as WorkspaceService makes them
    private static string WorkspaceId(string tenant)
        => string.Concat(tenant, Guid.NewGuid().ToString("n").AsSpan(tenant.Length));

    [Theory]
    [InlineData("")]
    [InlineData("zz")]
    public async Task EveryStoredIdFormFindsItsOwner(string tenant)
    {
        string current = WorkspaceId(tenant);
        string dashed = Guid.NewGuid().ToString("d");
        string template = Guid.NewGuid().ToString("n");
        string otherTenant = WorkspaceId(tenant == "" ? "qq" : "");
        // an import re-prefixed by TransferService.UpdateTenant
        string imported = string.Concat(tenant, Guid.NewGuid().ToString("d").AsSpan(tenant.Length));
        string dashedTemplate = Guid.NewGuid().ToString("d");

        _db.Workspaces.AddRange(
            new Workspace { Id = current, Name = "Current" },
            new Workspace { Id = dashed, Name = "Legacy" },
            new Workspace { Id = imported, Name = "Imported" });
        _db.Templates.AddRange(
            new Template { Id = template, Name = "Kali", WorkspaceId = current },
            new Template { Id = dashedTemplate, Name = "Old Kali", WorkspaceId = dashed });
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var hypervisor = FakeHypervisor.With(
            // vSphere: the folder names the workspace; a localized disk also names its template
            new TemplateSource { Name = "a", WorkspaceId = current },
            new TemplateSource { Name = "b", WorkspaceId = dashed },
            new TemplateSource { Name = "c", WorkspaceId = current, TemplateId = template },
            // Proxmox: a copy made on unlink is named by its template id
            new TemplateSource { Name = "d", TemplateId = dashedTemplate },
            // another TopoMojo's workspace on the same hypervisor is left out
            new TemplateSource { Name = "e", WorkspaceId = otherTenant },
            new TemplateSource { Name = "f", WorkspaceId = imported });

        var result = await TemplateSourcesSettingTests.Controller(
            new CoreOptions { EnableTemplateSources = true, Tenant = tenant },
            hypervisor, new TemplateStore(_db), new WorkspaceStore(_db)).GetTemplateSources();

        var report = Assert.IsType<TemplateSourceReport>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(
            new[] { "a Current ", "b Legacy ", "c Current Kali", "d Legacy Old Kali", "f Imported " },
            report.Sources.Select(s => $"{s.Name} {s.WorkspaceName} {s.TemplateName}"));
        Assert.All(report.Sources, s => Assert.False(s.IsGlobal));
    }

    public class FakeHypervisor : DispatchProxy
    {
        private TemplateSource[] _sources = [];

        public static IHypervisorService With(params TemplateSource[] sources)
        {
            var proxy = Create<IHypervisorService, FakeHypervisor>();
            ((FakeHypervisor)(object)proxy)._sources = sources;
            return proxy;
        }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
            => targetMethod.Name == nameof(IHypervisorService.GetTemplateSourceOptions)
                ? Task.FromResult(new TemplateSourceOptions { Field = "disks", Sources = _sources })
                : throw new NotSupportedException(targetMethod.Name);
    }
}
