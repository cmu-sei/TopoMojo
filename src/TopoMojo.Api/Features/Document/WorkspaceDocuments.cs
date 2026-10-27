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
    }
}
