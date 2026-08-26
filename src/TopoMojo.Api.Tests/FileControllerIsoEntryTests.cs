using System;
using System.IO;
using DiscUtils.Iso9660;
using TopoMojo.Api.Controllers;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class FileControllerIsoEntryTests
{
    [Fact]
    public void DatastoreUploadUsesOnlyLogicalIsoBasename()
    {
        const string workspaceId = "11111111-1111-1111-1111-111111111111";
        const string actorId = "22222222-2222-2222-2222-222222222222";
        const string datastorePath = $"iso/{workspaceId}/testing3.txt";
        string testRoot = Path.Combine(Path.GetTempPath(), "topomojo-api-tests", actorId);
        string sourcePath = Path.Combine(testRoot, "upload.bin");
        string isoPath = Path.Combine(testRoot, "upload.iso");

        try
        {
            Directory.CreateDirectory(testRoot);
            File.WriteAllText(sourcePath, "deterministic test payload");

            FileController.BuildIso(sourcePath, isoPath, datastorePath);

            using var isoStream = File.OpenRead(isoPath);
            using var reader = new CDReader(isoStream, true);
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
}
