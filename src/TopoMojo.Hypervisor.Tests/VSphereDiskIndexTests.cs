using TopoMojo.Hypervisor.vSphere;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class VSphereDiskIndexTests
{
    // progress per disk: -1 missing, 0-99 cloning, 100 present
    [Theory]
    [InlineData(new[] { -1, -1 }, new[] { 0, 1 })]
    [InlineData(new[] { 100, -1 }, new[] { 1 })]
    [InlineData(new[] { 100, -1, 50, -1 }, new[] { 1, 3 })]
    [InlineData(new[] { 40, 100, -1 }, new[] { 2 })]
    [InlineData(new[] { 100, 100 }, new int[0])]
    public void MissingDiskIndexes_FindsEveryMissingDisk(int[] progress, int[] expected)
    {
        Assert.Equal(expected, VSphereHypervisorService.MissingDiskIndexes(progress));
    }

    private const string Tag = "0123456789abcdef0123456789abcdef";

    private static VmDisk[] Disks(params string[] paths)
        => [.. System.Linq.Enumerable.Select(paths, p => new VmDisk { Path = p })];

    [Fact]
    public void DeletableDiskIndexes_KeepsCheckingAfterAStockDisk()
    {
        // the old loop stopped advancing after skipping disk 0, so disk 2 was judged by disk 0
        var disks = Disks("[ds] stock/base.vmdk", $"[ds] {Tag}/t_1.vmdk", $"[ds] {Tag}/t_2.vmdk");

        Assert.Equal(new[] { 1 }, VSphereHypervisorService.DeletableDiskIndexes(disks, [100, 100, 40], Tag));
    }

    [Fact]
    public void DeletableDiskIndexes_KeepsCheckingAfterAMissingDisk()
    {
        var disks = Disks($"[ds] {Tag}/t_0.vmdk", $"[ds] {Tag}/t_1.vmdk");

        Assert.Equal(new[] { 1 }, VSphereHypervisorService.DeletableDiskIndexes(disks, [-1, 100], Tag));
    }

    [Fact]
    public void DeletableDiskIndexes_NeverDeletesWithoutAnIsolationTag()
    {
        var disks = Disks($"[ds] {Tag}/t_0.vmdk");

        Assert.Empty(VSphereHypervisorService.DeletableDiskIndexes(disks, [100], null));
        Assert.Empty(VSphereHypervisorService.DeletableDiskIndexes(disks, [100], ""));
    }
}
