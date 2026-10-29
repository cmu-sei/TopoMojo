using System.Collections.Generic;
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
/// When import rewrites a workspace id to this instance's tenant, the workspace's document and
/// images must follow it, so the workspace can load them and deleting it removes them.
/// </summary>
public sealed class ImportedDocumentIdTests
{
    private const string Hex = "0123456789abcdef0123456789abcdef";
    private const string OtherTenantId = "xy" + "23456789abcdef0123456789abcdef";

    private static async Task<string> Import(DocRootFixture fixture, string sourceId, string tenant, string markdown = "# doc")
    {
        string repo = Path.Combine(Path.GetDirectoryName(fixture.Root), "repo");
        Directory.CreateDirectory(repo);

        using (var zip = ZipFile.Open(Path.Combine(repo, "test_export.zip"), ZipArchiveMode.Create))
        {
            // documents first, so the import can't rely on _data.json coming before them
            using (var writer = new StreamWriter(zip.CreateEntry(sourceId + ".md").Open()))
                await writer.WriteAsync(markdown);

            using (var writer = new StreamWriter(zip.CreateEntry(sourceId + "/image.png").Open()))
                await writer.WriteAsync("png");

            var data = new[] { new Data.Workspace { Id = sourceId, Name = "imported" } };
            using (var writer = new StreamWriter(zip.CreateEntry("_data.json").Open()))
                await writer.WriteAsync(JsonSerializer.Serialize(data));
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

    [Theory]
    // no tenant into a tenant
    [InlineData(Hex, "tm")]
    // another tenant into ours
    [InlineData(OtherTenantId, "tm")]
    // a tenant into an instance with none
    [InlineData("tm" + "23456789abcdef0123456789abcdef", "")]
    public async Task DocumentsFollowTheImportedId(string sourceId, string tenant)
    {
        using var fixture = new DocRootFixture();

        string importedId = await Import(fixture, sourceId, tenant);

        Assert.NotEqual(sourceId, importedId);
        Assert.True(File.Exists(fixture.DocPath(importedId)));
        Assert.True(File.Exists(Path.Combine(fixture.ImageDir(importedId), "image.png")));
        Assert.False(File.Exists(fixture.DocPath(sourceId)));
        Assert.False(Directory.Exists(fixture.ImageDir(sourceId)));
    }

    [Fact]
    public async Task ImageLinksFollowTheImportedId()
    {
        using var fixture = new DocRootFixture();
        string markdown =
            $"![relative](docs/{Hex}/image.png)\n" +
            $"![absolute](https://topomojo.example/docs/{Hex}/image.png)\n" +
            $"text mentioning {Hex} stays";

        string importedId = await Import(fixture, Hex, "tm", markdown);
        string imported = await File.ReadAllTextAsync(fixture.DocPath(importedId));

        Assert.Equal(
            $"![relative](docs/{importedId}/image.png)\n" +
            $"![absolute](https://topomojo.example/docs/{importedId}/image.png)\n" +
            $"text mentioning {Hex} stays",
            imported);
    }

    [Theory]
    [InlineData(Hex, "")]
    [InlineData("tm" + "23456789abcdef0123456789abcdef", "tm")]
    public async Task DocumentsKeepTheirNamesWithoutAnIdChange(string sourceId, string tenant)
    {
        using var fixture = new DocRootFixture();

        string importedId = await Import(fixture, sourceId, tenant, $"![i](docs/{sourceId}/image.png)");

        Assert.Equal(sourceId, importedId);
        Assert.Equal($"![i](docs/{sourceId}/image.png)", await File.ReadAllTextAsync(fixture.DocPath(sourceId)));
        Assert.True(File.Exists(Path.Combine(fixture.ImageDir(sourceId), "image.png")));
    }

    // the case from #136's review: deleting an imported workspace removes its files
    [Fact]
    public async Task DeletingAWorkspaceImportedFromAnotherTenantRemovesItsDocuments()
    {
        using var fixture = new DocRootFixture();
        string importedId = await Import(fixture, OtherTenantId, "tm");

        await new WorkspaceService(
            fixture.Store,
            null,
            NullHypervisor.Create(),
            NullLogger<WorkspaceService>.Instance,
            DocRootFixture.Mapper,
            new CoreOptions { DocPath = fixture.Root }
        ).Delete(importedId);

        Assert.Empty(Directory.GetFileSystemEntries(fixture.Root));
    }

    [Theory]
    [InlineData("aaaa.md", "bbbb.md", "aaaa")]
    [InlineData("aaaa/image.png", "bbbb/image.png", "aaaa")]
    [InlineData("aaaa/nested/image.png", "bbbb/nested/image.png", "aaaa")]
    [InlineData("cccc.md", "cccc.md", null)]
    [InlineData("aaaab.md", "aaaab.md", null)]
    [InlineData("aaaab/image.png", "aaaab/image.png", null)]
    public void RenamedDocumentEntry_MovesOnlyTheRenamedWorkspacesEntries(string entry, string expected, string expectedSource)
    {
        var renamed = new Dictionary<string, string> { ["aaaa"] = "bbbb" };

        var (name, sourceId) = TransferService.RenamedDocumentEntry(entry, renamed);

        Assert.Equal(expected, name);
        Assert.Equal(expectedSource, sourceId);
    }
}
