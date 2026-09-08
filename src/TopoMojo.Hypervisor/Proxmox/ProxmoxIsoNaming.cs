// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Text.RegularExpressions;
using TopoMojo.Hypervisor.Exceptions;

namespace TopoMojo.Hypervisor.Proxmox
{
    public static partial class ProxmoxIsoNaming
    {
        public const string DefaultScopeSeparator = "__";
        public const char LegacyScopeSeparator = '#';

        public static string NormalizeFilename(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("An ISO filename is required.", nameof(filename));

            var separator = filename.LastIndexOfAny(['/', '\\']);
            var name = filename[(separator + 1)..];
            name = InvalidFilenameCharsRegex().Replace(name, "_");
            return RepeatedUnderscoresRegex().Replace(name, "_");
        }

        public static string Encode(string scopeId, string filename, string separator)
        {
            if (string.IsNullOrEmpty(separator))
                throw new ArgumentException("An ISO scope separator is required.", nameof(separator));

            if (string.IsNullOrWhiteSpace(scopeId) || InvalidScopeCharsRegex().IsMatch(scopeId))
                throw new ArgumentException("The ISO scope id contains invalid characters.", nameof(scopeId));

            return $"{scopeId}{separator}{NormalizeFilename(filename)}";
        }

        /// <summary>
        /// Reproduces the stored filename TopoMojo wrote into a mounted flat ISO share before the scope
        /// separator became configurable: '{scopeId}#{filename with directories and spaces removed}'.
        /// Kept so ISOs uploaded by an earlier build stay readable and deletable.
        /// </summary>
        public static string EncodeLegacy(string scopeId, string filename)
        {
            if (string.IsNullOrWhiteSpace(scopeId) || InvalidScopeCharsRegex().IsMatch(scopeId))
                throw new ArgumentException("The ISO scope id contains invalid characters.", nameof(scopeId));

            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("An ISO filename is required.", nameof(filename));

            var start = filename.LastIndexOfAny(['/', '\\']) + 1;
            var name = filename[start..].Replace(" ", string.Empty);
            if (name.Length == 0)
                throw new ArgumentException("An ISO filename is required.", nameof(filename));

            return $"{scopeId}{LegacyScopeSeparator}{name}";
        }


        public static bool TryDecode(string storedName, string separator, out string scopeId, out string fileName)
        {
            scopeId = null;
            fileName = null;

            if (string.IsNullOrEmpty(storedName) || string.IsNullOrEmpty(separator))
                return false;

            // Try successive separator occurrences because a configured separator can occur inside a GUID.
            var start = 0;
            while (start < storedName.Length)
            {
                var separatorIndex = storedName.IndexOf(separator, start, StringComparison.Ordinal);
                var legacyIndex = storedName.IndexOf(LegacyScopeSeparator, start);
                var useLegacy = legacyIndex >= 0 && (separatorIndex < 0 || legacyIndex < separatorIndex);
                var index = useLegacy ? legacyIndex : separatorIndex;

                if (index < 0)
                    return false;

                var separatorLength = useLegacy ? 1 : separator.Length;
                var prefix = storedName[..index];
                var decodedFileName = storedName[(index + separatorLength)..];
                if (Guid.TryParse(prefix, out _) && decodedFileName.Length > 0)
                {
                    scopeId = prefix;
                    fileName = decodedFileName;
                    return true;
                }

                start = index + separatorLength;
            }

            return false;
        }

        /// <summary>
        /// Throws when the configured scope separator would not survive PVE's storage upload API, which
        /// rewrites every character outside [-a-zA-Z0-9_.] to '_'.
        /// </summary>
        public static void ValidateScopeSeparator(string separator)
        {
            if (string.IsNullOrEmpty(separator))
            {
                throw new HypervisorException(
                    "Pod__IsoScopeSeparator cannot be empty - it is what carries the workspace scope in a Proxmox ISO filename.");
            }


            if (InvalidSeparatorCharsRegex().IsMatch(separator))
            {
                throw new HypervisorException(
                    $"Pod__IsoScopeSeparator '{separator}' cannot be used, because Proxmox's storage upload API rewrites any character outside [-a-zA-Z0-9_.] to '_'. Use '__'.");
            }
        }


        public static string BuildVolumeId(string storage, string storedName)
            => $"{storage}:iso/{storedName}";

        public static string StorageName(string isoStore)
            => isoStore?.Trim('/') ?? string.Empty;

        /// <summary>
        /// Builds the logical Proxmox ISO path <c>{storage}/{scopeId}/{fileName}</c>. The basename is
        /// normalized to the PVE-safe value that will be stored.
        /// </summary>
        public static string BuildDatastorePath(string isoStore, string scopeId, string fileName)
        {
            if (!Guid.TryParse(scopeId, out _))
                throw new HypervisorException($"Unsupported Proxmox ISO scope: {scopeId}");

            if (string.IsNullOrWhiteSpace(fileName))
                throw new HypervisorException("An ISO filename is required.");

            var separator = fileName.LastIndexOfAny(['/', '\\']);
            var safeFileName = fileName[(separator + 1)..];
            if (string.IsNullOrWhiteSpace(safeFileName))
                throw new HypervisorException($"Unsupported Proxmox ISO filename: {fileName}");

            safeFileName = NormalizeFilename(safeFileName);

            return $"{StorageName(isoStore)}/{scopeId}/{safeFileName}";
        }

        public static bool TrySplitDatastorePath(
            string datastorePath,
            out string storage,
            out string scopeId,
            out string fileName)
        {
            storage = null;
            scopeId = null;
            fileName = null;

            var segments = datastorePath?.Split('/');
            if (segments is not { Length: 3 }
                || segments.Any(string.IsNullOrEmpty)
                || !Guid.TryParse(segments[1], out _))
            {
                return false;
            }

            storage = segments[0];
            scopeId = segments[1];
            fileName = segments[2];
            return true;
        }

        [GeneratedRegex("[^a-zA-Z0-9_.-]")]
        private static partial Regex InvalidFilenameCharsRegex();

        [GeneratedRegex("_{2,}")]
        private static partial Regex RepeatedUnderscoresRegex();

        [GeneratedRegex("[^a-zA-Z0-9.-]")]
        private static partial Regex InvalidScopeCharsRegex();

        [GeneratedRegex("[^-a-zA-Z0-9_.]")]
        private static partial Regex InvalidSeparatorCharsRegex();
    }
}
