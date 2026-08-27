using System;
using System.IO;
using DiscUtils.Iso9660;
using TopoMojo.Api.Controllers;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class FileControllerIsoEntryTests
{
    [Theory]
    [InlineData("some/dir/testing3.txt")]
    [InlineData(@"C:\fakepath\testing3.txt")]
    [InlineData("11111111-1111-1111-1111-111111111111/22222222-2222-2222-2222-222222222222/testing3.txt")]
    public void BuildIso_StripsAnyPathFromTheEntryNameSource(string entryNameSource)
    {
        const string workspaceId = "11111111-1111-1111-1111-111111111111";
        const string actorId = "22222222-2222-2222-2222-222222222222";
        string testRoot = Path.Combine(Path.GetTempPath(), "topomojo-api-tests", Guid.NewGuid().ToString());
        string sourcePath = Path.Combine(testRoot, "upload.bin");
        string isoPath = Path.Combine(testRoot, "upload.iso");

        try
        {
            Directory.CreateDirectory(testRoot);
            File.WriteAllText(sourcePath, "deterministic test payload");

            FileController.BuildIso(sourcePath, isoPath, entryNameSource);

            using var isoStream = File.OpenRead(isoPath);
            using var reader = new CDReader(isoStream, true);

            // A surviving separator would nest the payload in a subdirectory instead of the root.
            Assert.Empty(reader.GetDirectories("\\"));

            string[] entries = reader.GetFiles("\\");

            Assert.Single(entries);
            string entry = entries[0].TrimStart('\\', '/');
            if (entry.EndsWith(";1", StringComparison.Ordinal))
                entry = entry[..^2];

            Assert.Equal("testing3.txt", entry);
            Assert.DoesNotContain(workspaceId, entry, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(actorId, entry, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void BuildIso_UsesTheSanitizedUserFilenameAsTheEntryName()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "topomojo-api-tests", Guid.NewGuid().ToString());
        string sourcePath = Path.Combine(testRoot, "upload.bin");
        string isoPath = Path.Combine(testRoot, "upload.iso");

        try
        {
            Directory.CreateDirectory(testRoot);
            File.WriteAllText(sourcePath, "deterministic test payload");

            FileController.BuildIso(sourcePath, isoPath, "My Notes.txt");

            using var isoStream = File.OpenRead(isoPath);
            using var reader = new CDReader(isoStream, true);
            string[] entries = reader.GetFiles("\\");

            Assert.Single(entries);
            string entry = entries[0].TrimStart('\\', '/');
            if (entry.EndsWith(";1", StringComparison.Ordinal))
                entry = entry[..^2];

            Assert.Equal("MyNotes.txt", entry);
        }
        finally
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveExistingIsoPathFallsBackToLegacyScopeSeparator()
    {
        const string workspaceId = "33333333-3333-3333-3333-333333333333";
        string current = $"{workspaceId}__MyFile.iso";
        string legacy = $"{workspaceId}#MyFile.iso";
        string isoRoot = Path.Combine(Path.GetTempPath(), "topomojo-api-tests", Guid.NewGuid().ToString());

        try
        {
            Directory.CreateDirectory(isoRoot);

            Assert.Null(FileController.ResolveExistingIsoPath(isoRoot, [current, legacy]));

            File.WriteAllText(Path.Combine(isoRoot, legacy), "legacy");
            Assert.Equal(
                Path.Combine(isoRoot, legacy),
                FileController.ResolveExistingIsoPath(isoRoot, [current, legacy]));

            File.WriteAllText(Path.Combine(isoRoot, current), "current");
            Assert.Equal(
                Path.Combine(isoRoot, current),
                FileController.ResolveExistingIsoPath(isoRoot, [current, legacy]));
        }
        finally
        {
            if (Directory.Exists(isoRoot))
                Directory.Delete(isoRoot, recursive: true);
        }
    }
}
