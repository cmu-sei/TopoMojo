using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TopoMojo.Api.Data;
using TopoMojo.Api.Services;
using Xunit;

namespace TopoMojo.Api.Tests;

/// <summary>
/// Import rewrites a workspace id to this instance's tenant. The workspace's document and images
/// must follow the new id, or the workspace can't load them.
/// </summary>
public sealed class ImportDocumentIdTests : IDisposable
{
    private const string Hex = "0123456789abcdef0123456789abcdef";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "topomojo-api-tests", Guid.NewGuid().ToString());

    private readonly TopoMojoDbContextInMemory _db = new(
        new DbContextOptionsBuilder<TopoMojoDbContextInMemory>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options
    );

    private static readonly IMapper Mapper = new MapperConfiguration(
        cfg => cfg.AddMaps(typeof(TransferService).Assembly)
    ).CreateMapper();

    private string DocRoot => Path.Combine(_root, "docs");

    public ImportDocumentIdTests()
    {
        Directory.CreateDirectory(DocRoot);
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private async Task Import(string tenant, params (string Name, string Content)[] entries)
    {
        string repo = Path.Combine(_root, "repo");
        Directory.CreateDirectory(repo);

        using (var zip = ZipFile.Open(Path.Combine(repo, "test_export.zip"), ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(name).Open());
                await writer.WriteAsync(content);
            }
        }

        var transfer = new TransferService(
            new WorkspaceStore(_db),
            new TemplateStore(_db),
            NullLogger<TransferService>.Instance,
            Mapper,
            new CoreOptions { DocPath = DocRoot, Tenant = tenant }
        );

        await transfer.Import(repo, DocRoot);
    }

    private static (string, string) Data(params string[] ids) => (
        "_data.json",
        JsonSerializer.Serialize(ids.Select(id => new Data.Workspace { Id = id, Name = id }).ToArray())
    );

    private static (string, string)[] Documents(string id) =>
    [
        (id + ".md", "# doc " + id),
        (id + "/image.png", "png " + id),
    ];

    private string Doc(string id) => Path.Combine(DocRoot, id + ".md");
    private string Image(string id) => Path.Combine(DocRoot, id, "image.png");

    [Theory]
    // No tenant into a tenant.
    [InlineData(Hex, "tm", "tm" + "23456789abcdef0123456789abcdef")]
    // Another tenant into ours.
    [InlineData("xy" + "23456789abcdef0123456789abcdef", "tm", "tm" + "23456789abcdef0123456789abcdef")]
    // A tenant into an instance with none: the prefix becomes zeros.
    [InlineData("tm" + "23456789abcdef0123456789abcdef", "", "00" + "23456789abcdef0123456789abcdef")]
    public async Task DocumentsFollowARewrittenWorkspaceId(string sourceId, string tenant, string expectedId)
    {
        await Import(tenant, [Data(sourceId), .. Documents(sourceId)]);

        Assert.Equal(expectedId, await _db.Workspaces.Select(w => w.Id).SingleAsync());
        Assert.Equal("# doc " + sourceId, File.ReadAllText(Doc(expectedId)));
        Assert.Equal("png " + sourceId, File.ReadAllText(Image(expectedId)));
        Assert.False(File.Exists(Doc(sourceId)));
        Assert.False(Directory.Exists(Path.Combine(DocRoot, sourceId)));
    }

    [Fact]
    public async Task DocumentsFollowTheIdWhenTheDataEntryComesLast()
    {
        string sourceId = Hex;
        string expectedId = "tm" + Hex[2..];

        await Import("tm", [.. Documents(sourceId), Data(sourceId)]);

        Assert.True(File.Exists(Doc(expectedId)));
        Assert.True(File.Exists(Image(expectedId)));
        Assert.False(File.Exists(Doc(sourceId)));
    }

    [Theory]
    [InlineData(Hex, "")]
    [InlineData("tm" + "23456789abcdef0123456789abcdef", "tm")]
    public async Task DocumentsKeepTheirNameWhenTheIdIsUnchanged(string sourceId, string tenant)
    {
        await Import(tenant, [Data(sourceId), .. Documents(sourceId)]);

        Assert.Equal(sourceId, await _db.Workspaces.Select(w => w.Id).SingleAsync());
        Assert.True(File.Exists(Doc(sourceId)));
        Assert.True(File.Exists(Image(sourceId)));
    }

    [Fact]
    public async Task EntriesThatBelongToNoImportedWorkspaceKeepTheirName()
    {
        await Import("tm", Data(Hex), ("README.md", "readme"), ("shared/logo.png", "logo"));

        Assert.True(File.Exists(Path.Combine(DocRoot, "README.md")));
        Assert.True(File.Exists(Path.Combine(DocRoot, "shared", "logo.png")));
    }
}
