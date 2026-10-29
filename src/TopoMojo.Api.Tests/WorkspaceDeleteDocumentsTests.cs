using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using TopoMojo.Api.Services;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class WorkspaceDeleteDocumentsTests
{
    private static WorkspaceService CreateService(DocRootFixture fixture) => new(
        fixture.Store,
        null,
        NullHypervisor.Create(),
        NullLogger<WorkspaceService>.Instance,
        DocRootFixture.Mapper,
        new CoreOptions { DocPath = fixture.Root }
    );

    [Fact]
    public async Task DeletingAWorkspaceRemovesItsDocumentAndImages()
    {
        using var fixture = new DocRootFixture();
        string id = await fixture.AddWorkspace();
        fixture.WriteDocuments(id);

        await CreateService(fixture).Delete(id);

        Assert.False(File.Exists(fixture.DocPath(id)));
        Assert.False(Directory.Exists(fixture.ImageDir(id)));
        Assert.Null(await fixture.Store.Retrieve(id));
    }

    [Fact]
    public async Task DeletingAWorkspaceLeavesOtherWorkspacesFiles()
    {
        using var fixture = new DocRootFixture();
        string id = await fixture.AddWorkspace();
        string other = await fixture.AddWorkspace();
        fixture.WriteDocuments(id);
        fixture.WriteDocuments(other);
        string unrelated = Path.Combine(fixture.Root, "README.md");
        File.WriteAllText(unrelated, "keep");

        await CreateService(fixture).Delete(id);

        Assert.True(File.Exists(fixture.DocPath(other)));
        Assert.True(File.Exists(Path.Combine(fixture.ImageDir(other), "image.png")));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public async Task DeletingAWorkspaceWithoutDocumentsSucceeds()
    {
        using var fixture = new DocRootFixture();
        string id = await fixture.AddWorkspace();

        await CreateService(fixture).Delete(id);

        Assert.Null(await fixture.Store.Retrieve(id));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("../outside")]
    [InlineData("sub/dir")]
    [InlineData(@"..\outside")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData(null)]
    public void RemoveRefusesAnIdThatIsNotASingleContainedSegment(string id)
    {
        using var fixture = new DocRootFixture();
        string parent = Path.GetDirectoryName(fixture.Root);
        File.WriteAllText(Path.Combine(parent, "outside.md"), "keep");
        Directory.CreateDirectory(Path.Combine(fixture.Root, "sub", "dir"));

        Assert.Empty(WorkspaceDocuments.Remove(fixture.Root, id));

        Assert.True(Directory.Exists(fixture.Root));
        Assert.True(File.Exists(Path.Combine(parent, "outside.md")));
        Assert.True(Directory.Exists(Path.Combine(fixture.Root, "sub", "dir")));
    }

    [Fact]
    public void RemoveLeavesASymlinkedImageDirectoryAndItsTarget()
    {
        using var fixture = new DocRootFixture();
        string id = "0123456789abcdef0123456789abcdef";
        string target = Path.Combine(Path.GetDirectoryName(fixture.Root), "elsewhere");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.png"), "png");
        Directory.CreateSymbolicLink(fixture.ImageDir(id), target);

        WorkspaceDocuments.Remove(fixture.Root, id);

        Assert.True(File.Exists(Path.Combine(target, "keep.png")));
    }

    [Fact]
    public void RemoveWorksWithARelativeDocRoot()
    {
        using var fixture = new DocRootFixture();
        string id = "0123456789abcdef0123456789abcdef";
        fixture.WriteDocuments(id);
        string relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), fixture.Root);

        string[] removed = WorkspaceDocuments.Remove(relative, id);

        Assert.Equal(2, removed.Length);
        Assert.False(File.Exists(fixture.DocPath(id)));
        Assert.False(Directory.Exists(fixture.ImageDir(id)));
    }
}
