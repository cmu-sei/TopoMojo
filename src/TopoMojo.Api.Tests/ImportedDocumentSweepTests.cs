using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TopoMojo.Api.Data;
using TopoMojo.Api.Services;
using Xunit;

namespace TopoMojo.Api.Tests;

/// <summary>
/// Import rewrites a workspace id to this instance's tenant. Whether the imported document files
/// land under the source id or the rewritten one, the orphan sweep must not delete them.
/// </summary>
public sealed class ImportedDocumentSweepTests
{
    private const string Hex = "0123456789abcdef0123456789abcdef";

    private static readonly JanitorOptions Live = new() { DryRun = false };

    private static async Task<string> Import(DocRootFixture fixture, string sourceId, string tenant)
    {
        string repo = Path.Combine(Path.GetDirectoryName(fixture.Root), "repo");
        Directory.CreateDirectory(repo);

        using (var zip = ZipFile.Open(Path.Combine(repo, "test_export.zip"), ZipArchiveMode.Create))
        {
            var data = new[] { new Data.Workspace { Id = sourceId, Name = "imported" } };
            using (var writer = new StreamWriter(zip.CreateEntry("_data.json").Open()))
                await writer.WriteAsync(JsonSerializer.Serialize(data));
            using (var writer = new StreamWriter(zip.CreateEntry(sourceId + ".md").Open()))
                await writer.WriteAsync("# doc");
            using (var writer = new StreamWriter(zip.CreateEntry(sourceId + "/image.png").Open()))
                await writer.WriteAsync("png");
        }

        var transfer = new TransferService(
            fixture.Store,
            new TemplateStore(fixture.Db),
            NullLogger<TransferService>.Instance,
            DocRootFixture.Mapper,
            new CoreOptions { DocPath = fixture.Root, Tenant = tenant }
        );

        await transfer.Import(repo, fixture.Root);

        return await fixture.Db.Workspaces.Select(w => w.Id).SingleAsync();
    }

    private static string[] Files(DocRootFixture fixture) =>
        [.. Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Order()];

    private static JanitorService CreateJanitor(DocRootFixture fixture, string tenant) => new(
        NullLogger<JanitorService>.Instance,
        new CoreOptions { DocPath = fixture.Root, Tenant = tenant },
        NullHypervisor.Create(),
        fixture.Store,
        null,
        new FileUploadOptions { DocRoot = fixture.Root }
    );

    [Theory]
    // No tenant into a tenant: the row gets the tenant, the files keep the all-hex source id.
    [InlineData(Hex, "tm")]
    // Another tenant into ours: the files keep the other tenant's prefix.
    [InlineData("xy" + "23456789abcdef0123456789abcdef", "tm")]
    // A tenant into an instance with none: the row prefix becomes zeros.
    [InlineData("tm" + "23456789abcdef0123456789abcdef", "")]
    // A hyphenated GUID id from an instance with no tenant, into a tenant.
    [InlineData("11111111-2222-3333-4444-555555555555", "tm")]
    public async Task SweepLeavesDocumentsImportedUnderAnotherInstancesId(string sourceId, string tenant)
    {
        using var fixture = new DocRootFixture();

        string importedId = await Import(fixture, sourceId, tenant);
        string[] imported = Files(fixture);
        var reports = await CreateJanitor(fixture, tenant).CleanupOrphanedDocuments(Live);

        Assert.NotEqual(sourceId, importedId);
        Assert.Empty(reports);
        Assert.Equal(2, imported.Length);
        Assert.Equal(imported, Files(fixture));
    }

    [Theory]
    [InlineData(Hex, "")]
    [InlineData("tm" + "23456789abcdef0123456789abcdef", "tm")]
    public async Task SweepKeepsDocumentsImportedWithoutAnIdChange(string sourceId, string tenant)
    {
        using var fixture = new DocRootFixture();

        string importedId = await Import(fixture, sourceId, tenant);
        var reports = await CreateJanitor(fixture, tenant).CleanupOrphanedDocuments(Live);

        Assert.Equal(sourceId, importedId);
        Assert.Empty(reports);
        Assert.True(File.Exists(fixture.DocPath(importedId)));
        Assert.True(File.Exists(Path.Combine(fixture.ImageDir(importedId), "image.png")));
    }
}
