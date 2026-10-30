using System.Text.Json;
using TopoMojo.Api.Services;
using Xunit;

namespace TopoMojo.Api.Tests;

public sealed class TemplateCloneTests
{
    private const string WorkspaceId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OriginalId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string CloneId = "cccccccccccccccccccccccccccccccc";

    private static JsonElement Localize(string detail, string workspaceId)
        => JsonDocument.Parse(TemplateService.LocalizeClonedDetail(detail, workspaceId, CloneId)).RootElement;

    [Fact]
    public void AStockTemplateCloneGetsItsOwnDiskCopiedFromTheOriginal()
    {
        var detail = Localize(
            """{ "Disks": [ { "Path": "[ds] 00000000-0000-0000-0000-000000000000/router.vmdk" } ] }""",
            null);

        var disk = detail.GetProperty("Disks")[0];
        Assert.Equal($"[ds] 00000000-0000-0000-0000-000000000000/{CloneId}_0.vmdk", disk.GetProperty("Path").GetString());
        Assert.Equal("[ds] 00000000-0000-0000-0000-000000000000/router.vmdk", disk.GetProperty("Source").GetString());
    }

    [Fact]
    public void AWorkspaceTemplateCloneGetsADiskInItsWorkspaceFolder()
    {
        var detail = Localize(
            $$"""{ "Disks": [ { "Path": "[ds] {{WorkspaceId}}/{{OriginalId}}_0.vmdk" } ] }""",
            WorkspaceId);

        var disk = detail.GetProperty("Disks")[0];
        Assert.Equal($"[ds] {WorkspaceId}/{CloneId}_0.vmdk", disk.GetProperty("Path").GetString());
        Assert.Equal($"[ds] {WorkspaceId}/{OriginalId}_0.vmdk", disk.GetProperty("Source").GetString());
    }

    [Fact]
    public void AProxmoxCloneGetsItsOwnTemplateVmClonedFromTheOriginals()
    {
        var detail = Localize(
            $$"""{ "Template": "{{OriginalId}}", "ParentTemplate": "Alpine-Disk", "Disks": [] }""",
            WorkspaceId);

        Assert.Equal(CloneId, detail.GetProperty("Template").GetString());
        Assert.Equal(OriginalId, detail.GetProperty("ParentTemplate").GetString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ALinkedTemplateWithNoDetailIsLeftAlone(string detail)
    {
        Assert.Equal(detail, TemplateService.LocalizeClonedDetail(detail, WorkspaceId, CloneId));
    }
}
