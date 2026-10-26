// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Buffers.Binary;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TopoMojo.Hypervisor.Exceptions;

namespace TopoMojo.Hypervisor.vSphere
{
    /// <summary>
    /// Reads a VMDK's createType, to catch hosted disks (Workstation, Fusion, VirtualBox, OVA
    /// exports) that ESXi can't attach until they're converted.
    /// </summary>
    public static partial class VmdkFormat
    {
        /// <summary>
        /// Bytes to read from the start of a vmdk: enough for a text descriptor, or for the
        /// descriptor a sparse file embeds after its header.
        /// </summary>
        public const int HeaderLength = 64 * 1024;

        // createTypes ESXi opens directly
        private static readonly string[] EsxiTypes =
        [
            "vmfs", "vmfsThin", "vmfsSparse", "vmfsRaw", "vmfsRawDeviceMap",
            "vmfsPassthroughRawDeviceMap", "seSparse", "vsanSparse"
        ];

        /// <summary>
        /// The createType from a vmdk's leading bytes, or null if none is found.
        /// </summary>
        public static string ReadCreateType(ReadOnlySpan<byte> header)
        {
            // a sparse extent ("KDMV") embeds its descriptor; a descriptor file is plain text
            string text = header.Length >= 4 && header[..4].SequenceEqual("KDMV"u8)
                ? ReadEmbeddedDescriptor(header)
                : Encoding.ASCII.GetString(header);

            var match = CreateTypeRegex().Match(text);
            return match.Success ? match.Groups[1].Value : null;
        }

        public static bool IsHosted(string createType)
            => createType is not null
            && !EsxiTypes.Contains(createType, StringComparer.OrdinalIgnoreCase);

        public static HypervisorException HostedDiskError(string path, string createType)
        {
            string file = new DatastorePath(path).File;
            string converted = file.Replace(".vmdk", "-esx.vmdk", StringComparison.OrdinalIgnoreCase);

            return new HypervisorException(
                $"Disk {path} is a hosted VMDK (createType \"{createType}\", as exported by VMware Workstation, "
                + "Fusion, VirtualBox, or an OVA), which ESXi can't attach. Convert it on an ESXi host with "
                + $"\"vmkfstools -i {file} {converted} -d thin\" and point the template at {converted}."
            );
        }

        /// <summary>
        /// ESXi's power-on/attach failure for a disk it can't open, e.g.
        /// "Unsupported or invalid disk type 7 for 'scsi0:0'. Ensure that the disk has been imported."
        /// </summary>
        public static bool IsUnsupportedDiskError(string message)
            => message is not null && UnsupportedDiskRegex().IsMatch(message);

        public static HypervisorException UnsupportedDiskError(string message)
            => new(
                $"{message} The disk is probably a hosted VMDK (exported by VMware Workstation, Fusion, "
                + "VirtualBox, or an OVA). Convert it on an ESXi host with \"vmkfstools -i <disk>.vmdk "
                + "<disk>-esx.vmdk -d thin\" and point the template at the converted disk."
            );

        private static string ReadEmbeddedDescriptor(ReadOnlySpan<byte> header)
        {
            // SparseExtentHeader: magic(4) version(4) flags(4) capacity(8) grainSize(8)
            // descriptorOffset(8) descriptorSize(8), offsets and sizes in 512-byte sectors
            if (header.Length < 44)
                return "";

            long offset = BinaryPrimitives.ReadInt64LittleEndian(header[28..36]) * 512;
            long size = BinaryPrimitives.ReadInt64LittleEndian(header[36..44]) * 512;

            if (offset <= 0 || size <= 0 || offset >= header.Length)
                return "";

            int length = (int)Math.Min(size, header.Length - offset);
            return Encoding.ASCII.GetString(header.Slice((int)offset, length));
        }

        [GeneratedRegex(@"^\s*createType\s*=\s*""([^""]+)""", RegexOptions.Multiline)]
        private static partial Regex CreateTypeRegex();

        [GeneratedRegex(@"unsupported or invalid disk type", RegexOptions.IgnoreCase)]
        private static partial Regex UnsupportedDiskRegex();
    }
}
