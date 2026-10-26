using TopoMojo.Hypervisor;
using TopoMojo.Hypervisor.vSphere;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class VSphereDiskOptionsTests
{
    [Fact]
    public void ToTemplateDiskPaths_ListsDescriptorsAsTemplatePaths()
    {
        var options = new HypervisorServiceConfiguration
        {
            DiskStore = "[topomojo]",
            VmStore = "[topomojo] _run/"
        };

        string[] files =
        [
            "[topomojo]/00000000-0000-0000-0000-000000000000/ubuntu.vmdk",
            "[topomojo]/00000000-0000-0000-0000-000000000000/ubuntu-flat.vmdk",
            "[topomojo] stock/kali.vmdk",
            "[topomojo] stock/kali-000001.vmdk",
            "[topomojo] stock/kali-ctk.vmdk",
            "[topomojo] _run/vm1#abc/vm1.vmdk",
            "[other] stock/elsewhere.vmdk"
        ];

        Assert.Equal(
            new[]
            {
                "[ds] 00000000-0000-0000-0000-000000000000/ubuntu.vmdk",
                "[ds] stock/kali.vmdk"
            },
            VSphereHypervisorService.ToTemplateDiskPaths(files, options));
    }

    [Fact]
    public void ToTemplateDiskPaths_IsRelativeToADiskStoreFolder()
    {
        var options = new HypervisorServiceConfiguration
        {
            DiskStore = "[san] disks/",
            VmStore = "[san] _run/"
        };

        string[] files =
        [
            "[san] disks/stock/win10.vmdk",
            "[san] other/notadisk.vmdk"
        ];

        Assert.Equal(
            new[] { "[ds] stock/win10.vmdk" },
            VSphereHypervisorService.ToTemplateDiskPaths(files, options));
    }
}
