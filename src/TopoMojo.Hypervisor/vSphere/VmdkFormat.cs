// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.RegularExpressions;
using TopoMojo.Hypervisor.Exceptions;

namespace TopoMojo.Hypervisor.vSphere
{
    /// <summary>
    /// Recognizes ESXi's power-on failure for hosted disks (Workstation, Fusion, VirtualBox, OVA
    /// exports) that it can't attach until they're converted.
    /// </summary>
    public static partial class VmdkFormat
    {
        /// <summary>
        /// Shown to the user; the disk, its format, and the fix go to the admin error log.
        /// </summary>
        public const string UserMessage =
            "This template's disk is in a format the hypervisor can't use. Ask an administrator to convert it.";

        // The vCenter disk copy Initialize runs for a disk with a Source converts a hosted source,
        // so no ESXi shell is needed (VMware Cloud on AWS has none).
        private static string ConvertHint(string source, string converted)
            => "To convert it, set the disk's Source to the uploaded file and its Path to a new file, then "
            + $"Initialize the template again, e.g. \"Disks\": [{{ \"Source\": \"{source}\", \"Path\": \"{converted}\" }}].";

        /// <summary>
        /// ESXi's power-on/attach failure for a disk it can't open, e.g.
        /// "Unsupported or invalid disk type 7 for 'scsi0:0'. Ensure that the disk has been imported."
        /// </summary>
        public static bool IsUnsupportedDiskError(string message)
            => message is not null && UnsupportedDiskRegex().IsMatch(message);

        public static HypervisorException UnsupportedDiskError(string message)
            => new(
                UserMessage,
                $"{message} The disk is probably a hosted VMDK (exported by VMware Workstation, Fusion, "
                + "VirtualBox, or an OVA). " + ConvertHint("<uploaded disk>.vmdk", "<uploaded disk>-esx.vmdk")
            );

        [GeneratedRegex(@"unsupported or invalid disk type", RegexOptions.IgnoreCase)]
        private static partial Regex UnsupportedDiskRegex();
    }
}
