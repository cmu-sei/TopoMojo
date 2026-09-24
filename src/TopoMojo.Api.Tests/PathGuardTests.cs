using System;
using System.IO;
using TopoMojo.Api.Extensions;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class PathGuardTests
{
    private const string WorkspaceId = "11111111-1111-1111-1111-111111111111";
    private const string OtherWorkspaceId = "22222222-2222-2222-2222-222222222222";

    [Theory]
    [InlineData("background.png")]
    [InlineData("My Notes.txt")]
    [InlineData("nested.name.with.dots.png")]
    public void ResolveContainedFilenameAcceptsPlainFilenames(string filename)
    {
        using var temp = new TempDir();
        string directory = temp.Dir(WorkspaceId);

        Assert.Equal(
            Path.Combine(directory, filename),
            PathGuard.ResolveContainedFilename(directory, filename));
    }

    [Theory]
    // A sibling workspace's document lives at DocRoot/<id>.md, one level above the image directory.
    [InlineData("../" + OtherWorkspaceId + ".md")]
    [InlineData("../../../appsettings.json")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("subdir/image.png")]
    // Backslash is a legal filename character on Linux, so sanitizing would not remove it.
    [InlineData(@"..\..\appsettings.json")]
    [InlineData("/etc/passwd")]
    [InlineData("")]
    [InlineData(null)]
    public void ResolveContainedFilenameRejectsAnythingOutsideTheDirectory(string filename)
    {
        using var temp = new TempDir();
        string directory = temp.Dir(WorkspaceId);

        Assert.Null(PathGuard.ResolveContainedFilename(directory, filename));
    }

    [Theory]
    // The shapes CreateZipfile actually writes: a root file, or one workspace-id subdirectory.
    [InlineData("_data.json")]
    [InlineData(WorkspaceId + ".md")]
    [InlineData(WorkspaceId + "/diagram.png")]
    public void ResolveContainedAcceptsLegitimateZipEntries(string entryName)
    {
        using var temp = new TempDir();

        Assert.Equal(
            Path.GetFullPath(Path.Combine(temp.Path, entryName)),
            PathGuard.ResolveContained(temp.Path, entryName));
    }

    [Theory]
    [InlineData("../../../home/app/appsettings.json")]
    [InlineData(WorkspaceId + "/../../escaped.md")]
    [InlineData("/etc/cron.d/payload")]
    [InlineData("")]
    [InlineData(null)]
    public void ResolveContainedRejectsZipEntriesThatEscape(string entryName)
    {
        using var temp = new TempDir();

        Assert.Null(PathGuard.ResolveContained(temp.Path, entryName));
    }

    [Fact]
    public void ResolveContainedHandlesARelativeRoot()
    {
        // FileUpload__DocRoot defaults to the relative path "wwwroot/docs".
        string root = Path.Combine("wwwroot", "docs");

        Assert.Null(PathGuard.ResolveContained(root, "../../../appsettings.json"));

        Assert.Equal(
            Path.GetFullPath(Path.Combine(root, "keep.md")),
            PathGuard.ResolveContained(root, "keep.md"));
    }

    [Fact]
    public void ResolveContainedTreatsARootWithATrailingSeparatorTheSame()
    {
        using var temp = new TempDir();

        Assert.Equal(
            PathGuard.ResolveContained(temp.Path, "keep.md"),
            PathGuard.ResolveContained(temp.Path + Path.DirectorySeparatorChar, "keep.md"));
    }

    [Fact]
    public void ResolveContainedDoesNotAcceptASiblingDirectoryWithTheRootAsANamePrefix()
    {
        using var temp = new TempDir();
        string root = temp.Dir("docs");
        temp.Dir("docs-backup");

        // A naive prefix check without the trailing separator would let "docs-backup" through.
        Assert.Null(PathGuard.ResolveContained(root, Path.Combine("..", "docs-backup", "file.md")));
    }

    [Fact]
    public void ATraversingNameWouldOtherwiseResolveOutsideTheRoot()
    {
        using var temp = new TempDir();
        string directory = temp.Dir(WorkspaceId);

        // Guards the premise of the tests above: Path.Combine alone does escape.
        Assert.Equal(
            Path.Combine(temp.Path, "victim.md"),
            Path.GetFullPath(Path.Combine(directory, "../victim.md")));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "topomojo-api-tests", Guid.NewGuid().ToString());

        public TempDir() => Directory.CreateDirectory(Path);

        public string Dir(string name)
        {
            string path = System.IO.Path.Combine(Path, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
