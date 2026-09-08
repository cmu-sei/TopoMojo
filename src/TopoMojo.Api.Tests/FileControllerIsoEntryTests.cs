using System;
using System.IO;
using DiscUtils.Iso9660;
using TopoMojo.Api.Controllers;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class FileControllerIsoEntryTests
{
    [Theory]
    [InlineData("some/dir/testing3.txt", "testing3.txt")]
    [InlineData(@"C:\fakepath\testing3.txt", "testing3.txt")]
    [InlineData("11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/testing3.txt", "testing3.txt")]
    [InlineData("My Notes.txt", "MyNotes.txt")]
    public void BuildIso_WritesTheSanitizedBasenameAsTheSoleRootEntry(string entryNameSource, string expectedEntry)
    {
        using var temp = new TempDir();
        string sourcePath = Path.Combine(temp.Path, "upload.bin");
        string isoPath = Path.Combine(temp.Path, "upload.iso");
        File.WriteAllText(sourcePath, "deterministic test payload");

        FileController.BuildIso(sourcePath, isoPath, entryNameSource);

        using var isoStream = File.OpenRead(isoPath);
        using var reader = new CDReader(isoStream, true);

        // A surviving path separator would nest the payload in a subdirectory instead of the root.
        Assert.Empty(reader.GetDirectories("\\"));

        string[] entries = reader.GetFiles("\\");
        Assert.Single(entries);

        string entry = entries[0].TrimStart('\\', '/');
        if (entry.EndsWith(";1", StringComparison.Ordinal))
            entry = entry[..^2];

        Assert.Equal(expectedEntry, entry);
    }

    [Fact]
    public void ResolveExistingIsoPathFallsBackToLegacyScopeSeparator()
    {
        const string workspaceId = "33333333-3333-3333-3333-333333333333";
        string current = $"{workspaceId}__MyFile.iso";
        string legacy = $"{workspaceId}#MyFile.iso";
        using var temp = new TempDir();

        Assert.Null(FileController.ResolveExistingIsoPath(temp.Path, [current, legacy]));

        File.WriteAllText(Path.Combine(temp.Path, legacy), "legacy");
        Assert.Equal(
            Path.Combine(temp.Path, legacy),
            FileController.ResolveExistingIsoPath(temp.Path, [current, legacy]));

        File.WriteAllText(Path.Combine(temp.Path, current), "current");
        Assert.Equal(
            Path.Combine(temp.Path, current),
            FileController.ResolveExistingIsoPath(temp.Path, [current, legacy]));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "topomojo-api-tests", Guid.NewGuid().ToString());

        public TempDir() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
