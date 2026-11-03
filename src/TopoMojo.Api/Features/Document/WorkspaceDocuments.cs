// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Services
{
    /// <summary>
    /// Filesystem artifacts a workspace keeps under the document root: the markdown document at
    /// {docRoot}/{id}.md and its uploaded images in {docRoot}/{id}/.
    /// </summary>
    public static class WorkspaceDocuments
    {
        /// <summary>
        /// Remove a workspace's markdown document and image directory, returning the paths removed.
        /// </summary>
        /// <remarks>
        /// Images are served from /docs/{id}/ without authorization, so they must not outlive the
        /// workspace. A workspace id is a single path segment; anything that would name a path, or
        /// resolve outside the document root, is refused rather than deleted. A symlinked image
        /// directory is left alone so the delete can't reach whatever it points at.
        /// </remarks>
        public static string[] Remove(string docRoot, string id)
        {
            List<string> removed = [];

            if (string.IsNullOrWhiteSpace(docRoot) || string.IsNullOrWhiteSpace(id))
                return [];

            string doc = PathGuard.ResolveContainedFilename(docRoot, id + ".md");
            string dir = PathGuard.ResolveContainedFilename(docRoot, id);

            if (doc is null || dir is null)
                return [];

            if (File.Exists(doc))
            {
                File.Delete(doc);
                removed.Add(doc);
            }

            if (Directory.Exists(dir) && new DirectoryInfo(dir).LinkTarget is null)
            {
                Directory.Delete(dir, recursive: true);
                removed.Add(dir);
            }

            return [.. removed];
        }

        /// <summary>
        /// List the top-level entries of the document root that look like workspace artifacts:
        /// {id}.md files and {id}/ directories whose name is shaped like a workspace id.
        /// </summary>
        /// <remarks>
        /// Anything else in the document root is not ours to judge and is never returned, nor is a
        /// symlink, nor an entry that doesn't resolve inside the root.
        /// </remarks>
        public static DocumentEntry[] FindEntries(string docRoot, string tenant)
        {
            if (string.IsNullOrWhiteSpace(docRoot) || !Directory.Exists(docRoot))
                return [];

            List<DocumentEntry> entries = [];

            foreach (string path in Directory.EnumerateFileSystemEntries(docRoot).Order(StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                FileSystemInfo info = Directory.Exists(path)
                    ? new DirectoryInfo(path)
                    : new FileInfo(path);

                if (info.LinkTarget is not null)
                    continue;

                bool isDirectory = info is DirectoryInfo;

                string id = isDirectory
                    ? name
                    : name.EndsWith(".md", StringComparison.Ordinal) ? name[..^3] : null;

                if (!IsWorkspaceId(id, tenant))
                    continue;

                string contained = PathGuard.ResolveContainedFilename(docRoot, name);

                if (contained is null)
                    continue;

                entries.Add(new DocumentEntry(id, contained, isDirectory, info.LastWriteTimeUtc));
            }

            return [.. entries];
        }

        /// <summary>
        /// Delete an entry returned by <see cref="FindEntries"/>.
        /// </summary>
        public static void Delete(DocumentEntry entry)
        {
            if (entry.IsDirectory)
            {
                if (Directory.Exists(entry.Path))
                    Directory.Delete(entry.Path, recursive: true);
            }
            else if (File.Exists(entry.Path))
            {
                File.Delete(entry.Path);
            }
        }

        /// <summary>
        /// Whether <paramref name="name"/> is shaped like one of this instance's workspace ids: 32
        /// characters made of the configured tenant prefix followed by hex characters (all 32 hex
        /// when no tenant is configured), or, only when no tenant is configured, a hyphenated GUID.
        /// </summary>
        /// <remarks>
        /// With a tenant configured, a 32-character name must start with it. Ids of an instance
        /// with no tenant, or with another tenant whose prefix happens to be hex, are also 32 hex
        /// characters, so accepting them would let a sweep over a shared document root delete
        /// another instance's documents. An instance with no tenant can't tell a hex-prefixed
        /// tenant's ids from its own, so tenants that share a document root should each set one.
        /// A hyphenated GUID carries no tenant prefix, so with a tenant configured it can't be one
        /// of this instance's ids; it is most likely a document imported under another
        /// instance's id.
        /// The all-zero id is the export format's stock-template placeholder, not a workspace, so
        /// it is never treated as one.
        /// </remarks>
        public static bool IsWorkspaceId(string name, string tenant)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            bool hasTenant = !string.IsNullOrEmpty(tenant);

            if (name.Length == 36)
                return !hasTenant && Guid.TryParseExact(name, "D", out Guid guid) && guid != Guid.Empty;

            if (name.Length != 32 || name.All(c => c == '0'))
                return false;

            if (hasTenant && !name.StartsWith(tenant, StringComparison.Ordinal))
                return false;

            string hex = hasTenant ? name[tenant.Length..] : name;

            return hex.Length > 0 && hex.All(Uri.IsHexDigit);
        }
    }

    /// <summary>
    /// A workspace artifact in the document root: its {id}.md file or its {id}/ image directory.
    /// </summary>
    public sealed record DocumentEntry(string Id, string Path, bool IsDirectory, DateTime LastWriteUtc)
    {
        public string Name => IsDirectory
            ? System.IO.Path.GetFileName(Path) + "/"
            : System.IO.Path.GetFileName(Path);
    }
}
