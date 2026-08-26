// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace TopoMojo.Hypervisor.Proxmox
{
    public static partial class ProxmoxIsoNaming
    {
        public const string ScopeSeparator = "__";
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

        public static string Encode(string scopeId, string filename)
        {
            if (string.IsNullOrWhiteSpace(scopeId) || InvalidScopeCharsRegex().IsMatch(scopeId))
                throw new ArgumentException("The ISO scope id contains invalid characters.", nameof(scopeId));

            return $"{scopeId}{ScopeSeparator}{NormalizeFilename(filename)}";
        }

        public static bool TryDecode(string storedName, out string scopeId, out string fileName)
        {
            scopeId = null;
            fileName = null;

            if (string.IsNullOrEmpty(storedName))
                return false;

            var separatorIndex = storedName.IndexOf(ScopeSeparator, StringComparison.Ordinal);
            var legacyIndex = storedName.IndexOf(LegacyScopeSeparator);
            var index = separatorIndex < 0
                ? legacyIndex
                : legacyIndex < 0
                    ? separatorIndex
                    : Math.Min(separatorIndex, legacyIndex);
            var separatorLength = index == separatorIndex ? ScopeSeparator.Length : 1;

            if (index < 0)
                return false;

            var prefix = storedName[..index];
            if (!Guid.TryParse(prefix, out _))
                return false;

            var decodedFileName = storedName[(index + separatorLength)..];
            if (decodedFileName.Length == 0)
                return false;

            scopeId = prefix;
            fileName = decodedFileName;
            return true;
        }

        public static string BuildVolumeId(string storage, string storedName)
            => $"{storage}:iso/{storedName}";

        public static string StorageName(string isoStore)
            => isoStore?.Trim('/') ?? string.Empty;

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
    }
}
