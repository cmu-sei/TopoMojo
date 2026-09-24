// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Extensions
{
    /// <summary>
    /// Containment checks for filesystem paths assembled from client-supplied input.
    /// </summary>
    public static class PathGuard
    {
        /// <summary>
        /// Resolve <paramref name="relative"/> underneath <paramref name="root"/>, returning null
        /// when the result would land outside <paramref name="root"/>.
        /// </summary>
        /// <remarks>
        /// Path.Combine does not normalize "..", so the combined path has to be resolved and then
        /// compared against the root. GetFullPath also covers a relative root, which matters because
        /// FileUpload__DocRoot defaults to "wwwroot/docs".
        /// </remarks>
        public static string ResolveContained(string root, string relative)
        {
            if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative))
                return null;

            string prefix = Path.GetFullPath(root);

            if (!prefix.EndsWith(Path.DirectorySeparatorChar))
                prefix += Path.DirectorySeparatorChar;

            string full = Path.GetFullPath(Path.Combine(prefix, relative));

            return full.StartsWith(prefix, StringComparison.Ordinal) ? full : null;
        }

        /// <summary>
        /// Resolve a single filename directly inside <paramref name="directory"/>, returning null for
        /// anything that names a path rather than a file.
        /// </summary>
        /// <remarks>
        /// Path.GetInvalidFileNameChars() is just { '\0', '/' } on Linux, so sanitizing a filename
        /// would leave a backslash intact. Reject both separators outright before checking containment.
        /// </remarks>
        public static string ResolveContainedFilename(string directory, string filename)
        {
            if (string.IsNullOrEmpty(filename) || filename.IndexOfAny(['/', '\\']) >= 0)
                return null;

            return ResolveContained(directory, filename);
        }
    }
}
