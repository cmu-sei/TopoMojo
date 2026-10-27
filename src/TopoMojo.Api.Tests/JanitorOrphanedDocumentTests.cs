using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TopoMojo.Api.Services;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class JanitorOrphanedDocumentTests
{
    private const string Orphan = "0123456789abcdef0123456789abcdef";

    private static JanitorService CreateJanitor(DocRootFixture fixture, string tenant = "") => new(
        NullLogger<JanitorService>.Instance,
        new CoreOptions { DocPath = fixture.Root, Tenant = tenant },
        NullHypervisor.Create(),
        fixture.Store,
        null,
        new FileUploadOptions { DocRoot = fixture.Root }
    );

    private static readonly JanitorOptions Live = new() { DryRun = false };

    [Fact]
    public async Task SweepRemovesTheDocumentAndImagesOfAWorkspaceThatNoLongerExists()
    {
        using var fixture = new DocRootFixture();
        fixture.WriteDocuments(Orphan);

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(Live);

        Assert.False(File.Exists(fixture.DocPath(Orphan)));
        Assert.False(Directory.Exists(fixture.ImageDir(Orphan)));

        var report = Assert.Single(reports);
        Assert.Equal("OrphanedDocument", report.Reason);
        Assert.Equal(Orphan, report.Id);
        Assert.Equal($"{Orphan}/, {Orphan}.md", report.Name);
    }

    [Fact]
    public async Task SweepLeavesTheFilesOfExistingWorkspaces()
    {
        using var fixture = new DocRootFixture();
        string live = await fixture.AddWorkspace();
        fixture.WriteDocuments(live);
        fixture.WriteDocuments(Orphan);

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(Live);

        Assert.True(File.Exists(fixture.DocPath(live)));
        Assert.True(File.Exists(Path.Combine(fixture.ImageDir(live), "image.png")));
        Assert.Equal(Orphan, Assert.Single(reports).Id);
    }

    [Fact]
    public async Task SweepMatchesWorkspaceIdsWithoutRegardToCase()
    {
        using var fixture = new DocRootFixture();
        string live = await fixture.AddWorkspace("0123456789ABCDEF0123456789ABCDEF");
        fixture.WriteDocuments(live.ToLowerInvariant());

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(Live);

        Assert.Empty(reports);
        Assert.True(File.Exists(fixture.DocPath(live.ToLowerInvariant())));
    }

    [Theory]
    // Not workspace-id shaped: someone else's content, so not the sweep's to remove.
    [InlineData("README.md")]
    [InlineData("logo.png")]
    [InlineData("0123456789abcdef0123456789abcde.md")]
    [InlineData("0123456789abcdef0123456789abcdef0.md")]
    [InlineData("0123456789abcdef0123456789abcdeg.md")]
    [InlineData("0123456789abcdef0123456789abcdef.txt")]
    [InlineData("0123456789abcdef0123456789abcdef.md.bak")]
    // The export format's all-zero stock placeholder.
    [InlineData("00000000000000000000000000000000.md")]
    [InlineData("00000000-0000-0000-0000-000000000000.md")]
    public async Task SweepLeavesFilesThatAreNotWorkspaceDocuments(string name)
    {
        using var fixture = new DocRootFixture();
        string path = Path.Combine(fixture.Root, name);
        File.WriteAllText(path, "keep");

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(Live);

        Assert.Empty(reports);
        Assert.True(File.Exists(path));
    }

    [Theory]
    [InlineData("shared-images")]
    [InlineData("0123456789abcdef0123456789abcde")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task SweepLeavesDirectoriesThatAreNotWorkspaceImageDirectories(string name)
    {
        using var fixture = new DocRootFixture();
        string dir = Path.Combine(fixture.Root, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "keep.png"), "png");

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(Live);

        Assert.Empty(reports);
        Assert.True(File.Exists(Path.Combine(dir, "keep.png")));
    }

    [Fact]
    public async Task SweepRecognizesHyphenatedGuidsAndTenantPrefixedIds()
    {
        using var fixture = new DocRootFixture();
        string guid = "11111111-2222-3333-4444-555555555555";
        string tenanted = "dev" + Orphan[3..];
        string otherTenant = "xyz" + Orphan[3..];
        fixture.WriteDocuments(guid);
        fixture.WriteDocuments(tenanted);
        File.WriteAllText(fixture.DocPath(otherTenant), "keep");

        var reports = await CreateJanitor(fixture, tenant: "dev").CleanupOrphanedDocuments(Live);

        Assert.Equal(
            new[] { guid, tenanted }.OrderBy(x => x),
            reports.Select(r => r.Id).OrderBy(x => x));
        Assert.False(File.Exists(fixture.DocPath(guid)));
        Assert.False(File.Exists(fixture.DocPath(tenanted)));
        // Another tenant's prefix isn't hex and isn't ours, so it isn't recognized.
        Assert.True(File.Exists(fixture.DocPath(otherTenant)));
    }

    [Fact]
    public async Task SweepWithATenantLeavesOtherInstancesHexIdsAlone()
    {
        using var fixture = new DocRootFixture();
        // Ids of an instance with no tenant, and of a tenant whose prefix is hex, are both
        // 32 hex characters; a "dev" instance sharing the document root must not take them.
        string untenanted = Orphan;
        string hexTenant = "ab" + Orphan[2..];
        string ours = "dev" + Orphan[3..];
        fixture.WriteDocuments(untenanted);
        fixture.WriteDocuments(hexTenant);
        fixture.WriteDocuments(ours);

        var reports = await CreateJanitor(fixture, tenant: "dev").CleanupOrphanedDocuments(Live);

        Assert.Equal([ours], reports.Select(r => r.Id));
        Assert.False(File.Exists(fixture.DocPath(ours)));
        Assert.True(File.Exists(fixture.DocPath(untenanted)));
        Assert.True(File.Exists(fixture.DocPath(hexTenant)));
    }

    [Fact]
    public async Task DryRunReportsOrphansWithoutRemovingThem()
    {
        using var fixture = new DocRootFixture();
        fixture.WriteDocuments(Orphan);

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(new JanitorOptions { DryRun = true });

        Assert.Equal(Orphan, Assert.Single(reports).Id);
        Assert.True(File.Exists(fixture.DocPath(Orphan)));
        Assert.True(Directory.Exists(fixture.ImageDir(Orphan)));
    }

    [Fact]
    public async Task SweepLeavesSymlinksAndWhatTheyPointAt()
    {
        using var fixture = new DocRootFixture();
        string target = Path.Combine(Path.GetDirectoryName(fixture.Root), "elsewhere");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.png"), "png");
        File.WriteAllText(Path.Combine(target, "keep.md"), "md");
        Directory.CreateSymbolicLink(fixture.ImageDir(Orphan), target);
        File.CreateSymbolicLink(fixture.DocPath(Orphan), Path.Combine(target, "keep.md"));

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(Live);

        Assert.Empty(reports);
        Assert.True(Directory.Exists(fixture.ImageDir(Orphan)));
        Assert.True(File.Exists(fixture.DocPath(Orphan)));
        Assert.True(File.Exists(Path.Combine(target, "keep.png")));
    }

    [Fact]
    public async Task SweepOnlyLooksAtTheTopLevelOfDocRoot()
    {
        using var fixture = new DocRootFixture();
        string live = await fixture.AddWorkspace();
        fixture.WriteDocuments(live);
        string nested = Path.Combine(fixture.ImageDir(live), Orphan + ".md");
        File.WriteAllText(nested, "keep");

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(Live);

        Assert.Empty(reports);
        Assert.True(File.Exists(nested));
    }

    [Fact]
    public async Task SweepLeavesNothingOutsideDocRoot()
    {
        using var fixture = new DocRootFixture();
        string parent = Path.GetDirectoryName(fixture.Root);
        string sibling = Path.Combine(parent, Orphan + ".md");
        File.WriteAllText(sibling, "keep");
        Directory.CreateDirectory(Path.Combine(parent, Orphan));

        var reports = await CreateJanitor(fixture).CleanupOrphanedDocuments(Live);

        Assert.Empty(reports);
        Assert.True(File.Exists(sibling));
        Assert.True(Directory.Exists(Path.Combine(parent, Orphan)));
    }

    [Fact]
    public async Task SweepToleratesAMissingDocRoot()
    {
        using var fixture = new DocRootFixture();
        Directory.Delete(fixture.Root);

        Assert.Empty(await CreateJanitor(fixture).CleanupOrphanedDocuments(Live));
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", "", true)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF", "", true)]
    [InlineData("11111111-2222-3333-4444-555555555555", "", true)]
    [InlineData("dev456789abcdef0123456789abcdef", "dev", false)]
    [InlineData("xyz3456789abcdef0123456789abcdef", "xyz", true)]
    [InlineData("xyz3456789abcdef0123456789abcdef", "", false)]
    [InlineData("../456789abcdef0123456789abcdef01", "", false)]
    [InlineData("..", "", false)]
    [InlineData(null, "", false)]
    public void IsWorkspaceIdRecognizesOnlyIdShapedNames(string name, string tenant, bool expected)
    {
        Assert.Equal(expected, WorkspaceDocuments.IsWorkspaceId(name, tenant));
    }
}
