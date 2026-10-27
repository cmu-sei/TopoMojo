using System;
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

public sealed class WorkspaceDownloadPermissionTests : IDisposable
{
    private const string ManagerId = "manager-subject";
    private const string EditorId = "editor-subject";
    private const string StrangerId = "stranger-subject";

    private const string OwnWorkspaceId = "11111111111111111111111111111111";
    private const string OtherOwnWorkspaceId = "22222222222222222222222222222222";
    private const string ParentWorkspaceId = "33333333333333333333333333333333";
    private const string LinkedTemplateId = "44444444444444444444444444444444";
    private const string PublishedParentTemplateId = "55555555555555555555555555555555";
    private const string PrivateParentTemplateId = "66666666666666666666666666666666";
    private const string StockTemplateId = "77777777777777777777777777777777";
    private const string StockLinkTemplateId = "88888888888888888888888888888888";

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly string _docRoot = Path.Combine(
        Path.GetTempPath(), "topomojo-api-tests", Guid.NewGuid().ToString());

    public WorkspaceDownloadPermissionTests()
    {
        Directory.CreateDirectory(_docRoot);
        Seed();
    }

    public void Dispose()
    {
        if (Directory.Exists(_docRoot))
            Directory.Delete(_docRoot, recursive: true);
    }

    [Fact]
    public async Task ManagerCanDownloadTheirWorkspace()
    {
        Assert.True(await NewWorkspaceService().CanManageAll([OwnWorkspaceId], ManagerId));
    }

    [Fact]
    public async Task ManagerCanDownloadSeveralManagedWorkspaces()
    {
        Assert.True(await NewWorkspaceService().CanManageAll([OwnWorkspaceId, OtherOwnWorkspaceId], ManagerId));
    }

    [Fact]
    public async Task EditorWhoIsNotManagerIsDenied()
    {
        Assert.False(await NewWorkspaceService().CanManageAll([OwnWorkspaceId], EditorId));
    }

    [Fact]
    public async Task NonMemberIsDenied()
    {
        Assert.False(await NewWorkspaceService().CanManageAll([OwnWorkspaceId], StrangerId));
    }

    [Theory]
    [InlineData(OwnWorkspaceId, ParentWorkspaceId)]
    [InlineData(ParentWorkspaceId, OwnWorkspaceId)]
    [InlineData(OwnWorkspaceId, "99999999999999999999999999999999")]
    public async Task MixOfManagedAndOtherIdsIsDenied(string first, string second)
    {
        Assert.False(await NewWorkspaceService().CanManageAll([first, second], ManagerId));
    }

    [Fact]
    public async Task EmptyOrNullListIsDenied()
    {
        var svc = NewWorkspaceService();
        Assert.False(await svc.CanManageAll([], ManagerId));
        Assert.False(await svc.CanManageAll(null, ManagerId));
    }

    [Fact]
    public async Task AllKeywordIsDenied()
    {
        Assert.False(await NewWorkspaceService().CanManageAll(["all"], ManagerId));
    }

    [Fact]
    public async Task ManagerPackageHoldsOnlyTheLinkedTemplateOfAnUnmanagedParent()
    {
        var package = await Download([OwnWorkspaceId], ManagerId);

        var own = package.Workspace(OwnWorkspaceId);
        Assert.Equal("own challenge", own.GetProperty("Challenge").GetString());
        Assert.Contains($"{OwnWorkspaceId}.md", package.Entries);
        Assert.Contains($"{OwnWorkspaceId}/own.png", package.Entries);

        var parent = package.Workspace(ParentWorkspaceId);
        Assert.Equal("Parent Workspace", parent.GetProperty("Name").GetString());
        Assert.Equal(JsonValueKind.Null, parent.GetProperty("Challenge").ValueKind);
        Assert.Equal(JsonValueKind.Null, parent.GetProperty("Description").ValueKind);
        Assert.Equal([PublishedParentTemplateId], package.TemplateIds(ParentWorkspaceId));
        Assert.DoesNotContain($"{ParentWorkspaceId}.md", package.Entries);
        Assert.DoesNotContain($"{ParentWorkspaceId}/secret.png", package.Entries);
        Assert.DoesNotContain("parent secret", package.Json);

        // the stock template stays so the package imports cleanly
        Assert.Equal([StockTemplateId], package.TemplateIds(Guid.Empty.ToString()));
    }

    [Fact]
    public async Task ManagerOfTheParentGetsTheWholeParent()
    {
        await AddWorker(ParentWorkspaceId, ManagerId, Permission.Manager);

        var package = await Download([OwnWorkspaceId], ManagerId);

        AssertWholeParent(package);
    }

    [Fact]
    public async Task AdminPackageIsUnchanged()
    {
        var package = await Download([OwnWorkspaceId], null);

        AssertWholeParent(package);
    }

    private static void AssertWholeParent(Package package)
    {
        var parent = package.Workspace(ParentWorkspaceId);
        Assert.Equal("parent secret", parent.GetProperty("Challenge").GetString());
        Assert.Equal(
            new[] { PrivateParentTemplateId, PublishedParentTemplateId }.Order(),
            package.TemplateIds(ParentWorkspaceId).Order());
        Assert.Contains($"{ParentWorkspaceId}.md", package.Entries);
        Assert.Contains($"{ParentWorkspaceId}/secret.png", package.Entries);
    }

    private TopoMojoDbContext NewContext()
    {
        return new TopoMojoDbContext(
            new DbContextOptionsBuilder<TopoMojoDbContext>()
                .UseInMemoryDatabase(_databaseName)
                .Options
        );
    }

    private WorkspaceService NewWorkspaceService()
    {
        return new WorkspaceService(
            new WorkspaceStore(NewContext()),
            null,
            null,
            NullLogger<WorkspaceService>.Instance,
            null,
            new CoreOptions()
        );
    }

    private async Task<Package> Download(string[] ids, string managerId)
    {
        var db = NewContext();
        var svc = new TransferService(
            new WorkspaceStore(db),
            new TemplateStore(db),
            NullLogger<TransferService>.Instance,
            null,
            new CoreOptions()
        );

        using var stream = await svc.Download(ids, _docRoot, managerId);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("_data.json").Open());
        string json = await reader.ReadToEndAsync();
        return new Package(
            json,
            JsonDocument.Parse(json).RootElement.Clone(),
            zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToArray()
        );
    }

    private async Task AddWorker(string workspaceId, string subjectId, Permission permission)
    {
        using var db = NewContext();
        db.Workers.Add(new Worker { WorkspaceId = workspaceId, SubjectId = subjectId, Permission = permission });
        await db.SaveChangesAsync();
    }

    private void Seed()
    {
        using var db = NewContext();

        db.Templates.Add(new Template { Id = StockTemplateId, Name = "stock", IsPublished = true });

        db.Workspaces.Add(new Workspace
        {
            Id = ParentWorkspaceId,
            Name = "Parent Workspace",
            Description = "parent description",
            Challenge = "parent secret",
            Workers = [new Worker { SubjectId = StrangerId, Permission = Permission.Manager }],
            Templates =
            [
                new Template { Id = PublishedParentTemplateId, Name = "published", IsPublished = true },
                new Template { Id = PrivateParentTemplateId, Name = "private" }
            ]
        });

        db.Workspaces.Add(new Workspace
        {
            Id = OwnWorkspaceId,
            Name = "Own Workspace",
            Challenge = "own challenge",
            Workers =
            [
                new Worker { SubjectId = ManagerId, Permission = Permission.Manager },
                new Worker { SubjectId = EditorId, Permission = Permission.Editor }
            ],
            Templates =
            [
                new Template { Id = LinkedTemplateId, Name = "linked", IsLinked = true, ParentId = PublishedParentTemplateId },
                new Template { Id = StockLinkTemplateId, Name = "stock-link", IsLinked = true, ParentId = StockTemplateId }
            ]
        });

        db.Workspaces.Add(new Workspace
        {
            Id = OtherOwnWorkspaceId,
            Name = "Other Own Workspace",
            Workers = [new Worker { SubjectId = ManagerId, Permission = Permission.Manager }]
        });

        db.SaveChanges();

        WriteDoc(OwnWorkspaceId, "own.png");
        WriteDoc(ParentWorkspaceId, "secret.png");
    }

    private void WriteDoc(string workspaceId, string asset)
    {
        File.WriteAllText(Path.Combine(_docRoot, workspaceId + ".md"), "# doc");
        Directory.CreateDirectory(Path.Combine(_docRoot, workspaceId));
        File.WriteAllText(Path.Combine(_docRoot, workspaceId, asset), "asset");
    }

    private sealed record Package(string Json, JsonElement Data, string[] Entries)
    {
        public JsonElement Workspace(string id)
        {
            return Data.EnumerateArray().Single(w => w.GetProperty("Id").GetString() == id);
        }

        public IEnumerable<string> TemplateIds(string workspaceId)
        {
            return Workspace(workspaceId)
                .GetProperty("Templates")
                .EnumerateArray()
                .Select(t => t.GetProperty("Id").GetString())
                .ToArray();
        }
    }
}
