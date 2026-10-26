using System;
using System.Buffers.Binary;
using System.Text;
using TopoMojo.Hypervisor.vSphere;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class VmdkFormatTests
{
    private static string Descriptor(string createType) =>
        "# Disk DescriptorFile\n"
        + "version=1\n"
        + "CID=fffffffe\n"
        + "parentCID=ffffffff\n"
        + $"createType=\"{createType}\"\n"
        + "\n"
        + "# Extent description\n"
        + "RW 20971520 VMFS \"ubuntu-flat.vmdk\"\n";

    // A sparse extent: SparseExtentHeader with the descriptor embedded at sector 1.
    private static byte[] SparseExtent(string createType)
    {
        byte[] descriptor = Encoding.ASCII.GetBytes(Descriptor(createType));
        byte[] file = new byte[512 + 1024];
        Encoding.ASCII.GetBytes("KDMV").CopyTo(file, 0);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(4), 3);
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(28), 1); // descriptorOffset
        BinaryPrimitives.WriteInt64LittleEndian(file.AsSpan(36), 2); // descriptorSize
        descriptor.CopyTo(file, 512);
        return file;
    }

    [Theory]
    [InlineData("vmfs")]
    [InlineData("monolithicFlat")]
    [InlineData("twoGbMaxExtentSparse")]
    public void ReadCreateType_ReadsADescriptorFile(string createType)
    {
        Assert.Equal(createType, VmdkFormat.ReadCreateType(Encoding.ASCII.GetBytes(Descriptor(createType))));
    }

    [Theory]
    [InlineData("monolithicSparse")]
    [InlineData("streamOptimized")]
    public void ReadCreateType_ReadsTheDescriptorASparseExtentEmbeds(string createType)
    {
        Assert.Equal(createType, VmdkFormat.ReadCreateType(SparseExtent(createType)));
    }

    [Fact]
    public void ReadCreateType_IsNullWhenThereIsNoDescriptor()
    {
        Assert.Null(VmdkFormat.ReadCreateType(new byte[4096]));
        Assert.Null(VmdkFormat.ReadCreateType(Encoding.ASCII.GetBytes("KDMV")));
        Assert.Null(VmdkFormat.ReadCreateType([]));
    }

    [Theory]
    [InlineData("vmfs", false)]
    [InlineData("vmfsSparse", false)]
    [InlineData("seSparse", false)]
    [InlineData("vsanSparse", false)]
    [InlineData("monolithicSparse", true)]
    [InlineData("monolithicFlat", true)]
    [InlineData("streamOptimized", true)]
    [InlineData("twoGbMaxExtentSparse", true)]
    [InlineData("twoGbMaxExtentFlat", true)]
    [InlineData(null, false)]
    public void IsHosted_FlagsFormatsEsxiCantOpen(string createType, bool hosted)
    {
        Assert.Equal(hosted, VmdkFormat.IsHosted(createType));
    }

    [Fact]
    public void HostedDiskError_NamesTheDiskTypeAndConversion()
    {
        var ex = VmdkFormat.HostedDiskError("[topomojo] uploads/win10.vmdk", "monolithicSparse");

        Assert.Contains("[topomojo] uploads/win10.vmdk", ex.Message);
        Assert.Contains("monolithicSparse", ex.Message);
        Assert.Contains("vmkfstools -i win10.vmdk win10-esx.vmdk -d thin", ex.Message);
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
