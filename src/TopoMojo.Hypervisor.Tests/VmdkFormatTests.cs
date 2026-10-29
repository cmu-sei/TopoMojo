using TopoMojo.Hypervisor.vSphere;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class VmdkFormatTests
{
    [Fact]
    public void UnsupportedDiskError_KeepsTheEsxiMessageForAdmins()
    {
        var ex = VmdkFormat.UnsupportedDiskError("Unsupported or invalid disk type 7 for 'scsi0:0'.");

        Assert.Equal(VmdkFormat.UserMessage, ex.Message);
        Assert.StartsWith("Unsupported or invalid disk type 7 for 'scsi0:0'.", ex.Detail);
        Assert.Contains("Initialize the template again", ex.Detail);
    }

    [Theory]
    [InlineData("Unsupported or invalid disk type 7 for 'scsi0:0'. Ensure that the disk has been imported.", true)]
    [InlineData("Failed to open disk scsi0:0: Unsupported or invalid disk type 2.", true)]
    [InlineData("The attempted operation cannot be performed in the current state (Powered on).", false)]
    [InlineData(null, false)]
    public void IsUnsupportedDiskError_MatchesEsxiDiskTypeFailures(string message, bool expected)
    {
        Assert.Equal(expected, VmdkFormat.IsUnsupportedDiskError(message));
    }
}
