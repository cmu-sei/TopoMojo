// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using DiscUtils.Iso9660;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Services;
using TopoMojo.Api.Hubs;
using Swashbuckle.AspNetCore.Annotations;

namespace TopoMojo.Api.Controllers;

[Authorize]
[ApiController]
public class FileController(
    ILogger<AdminController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    IFileUploadHandler uploader,
    IFileUploadMonitor monitor,
    FileUploadOptions uploadOptions,
    WorkspaceService workspaceService,
    TopoMojo.Hypervisor.IHypervisorService hypervisorService,
    ITemplateStore templateStore,
    IGamespaceStore gamespaceStore
    ) : BaseController(logger, hub)
{
    private static class Meta
    {
        public const string OriginalName = "original-name";
        public const string Name = "name";
        public const string GroupKey = "group-key";
        public const string Size = "size";
        public const string DestinationPath = "destination-path";
        public const string DatastorePath = "DatastorePath";
        public const string IsoVolumeId = "UploadedFile";
        public const string IsoFileExtension = ".iso";
    }

    /// <summary>
    /// Get file upload progress.
    /// </summary>
    /// <remarks>
    /// If a client doesn't track progress client-side,
    /// it can specify a form value `monitor-key` with
    /// the file upload, and then query for progress
    /// using that key.
    /// </remarks>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("api/file/progress/{id}")]
    [SwaggerOperation(OperationId = "CheckFileUploadProgress")]
    [ProducesResponseType(typeof(int), 200)]
    public IActionResult CheckFileUploadProgress(string id)
    {
        return Json(monitor.Check(id).Progress);
    }

    /// <summary>
    /// Upload a file.
    /// </summary>
    /// <remarks>
    /// Expects mime-multipart body with single file
    /// and form-data part with:
    /// - group-key (optional guid specifying destination bin; defaults to public-bin)
    /// - monitor-key (optional unique value with which to query upload progress)
    /// - size (number of bytes in file)
    /// </remarks>
    /// <returns></returns>
    [HttpPost("api/file/upload")]
    [SwaggerOperation(OperationId = "UploadWorkspaceFile")]
    [DisableFormValueModelBinding]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<bool>> UploadWorkspaceFile()
    {
        await uploader.Process(
            Request,
            metadata =>
            {
                string publicTarget = Guid.Empty.ToString();
                string original = metadata[Meta.OriginalName];
                string filename = metadata[Meta.Name] ?? original;
                string key = metadata[Meta.GroupKey] ?? publicTarget;
                long size = Int64.Parse(metadata[Meta.Size] ?? "0");

                if (uploadOptions.MaxFileBytes > 0 && size > uploadOptions.MaxFileBytes)
                    throw new Exception($"File {filename} exceeds the {uploadOptions.MaxFileBytes} byte maximum size.");

                if (key != publicTarget && !workspaceService.CanEdit(key, Actor.Id).Result && !Actor.IsAdmin)
                    throw new InvalidOperationException();

                string dest;
                if (uploadOptions.UseDatastoreApi)
                {
                    dest = BuildTempPath(filename, key);
                    string datastorePath = ConvertToDatastorePath(filename, key);
                    metadata.Add(Meta.DatastorePath, datastorePath);
                }
                else
                {
                    dest = BuildDestinationPath(filename, key);
                }

                metadata.Add(Meta.DestinationPath, dest);
                Log("uploading", null, dest);

                return System.IO.File.Create(dest);
            },
            status =>
            {
                if (status.Error != null)
                {
                    string dp = status.Metadata[Meta.DestinationPath];
                    if (System.IO.File.Exists(dp))
                        System.IO.File.Delete(dp);
                }
                monitor.Update(status.Key, status.Progress);
                // TODO: broadcast progress to group
            },

            options =>
            {
                options.MultipartBodyLengthLimit = (long)((uploadOptions.MaxFileBytes > 0) ? uploadOptions.MaxFileBytes : 1E9);
            },

            async metadata =>
            {
                string dp = metadata[Meta.DestinationPath];

                if (uploadOptions.UseDatastoreApi)
                {
                    string datastorePath = metadata[Meta.DatastorePath];

                    try
                    {
                        await UploadToDatastore(dp, datastorePath);
                    }
                    catch (Exception ex)
                    {
                        Log("upload failed", null, ex.Message);
                        CleanupTempFiles(dp);
                        throw;
                    }
                }
                else
                {
                    if (!dp.ToLower().EndsWith(Meta.IsoFileExtension) && System.IO.File.Exists(dp))
                    {
                        CDBuilder builder = new()
                        {
                            UseJoliet = true,
                            VolumeIdentifier = Meta.IsoVolumeId
                        };
                        builder.AddFile(Path.GetFileName(dp), dp);
                        builder.Build(dp + Meta.IsoFileExtension);
                        System.IO.File.Delete(dp);
                    }
                }
            }
        );

        return Json(true);
    }

    private string BuildDestinationPath(string filename, string key)
    {
        if (uploadOptions.SupportsSubfolders)
        {
            string path = Path.Combine(
                uploadOptions.IsoRoot,
                key.SanitizePath()
            );

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return Path.Combine(
                path,
                SanitizeIsoFilename(filename)
            );
        }
        else
        {
            var fileName = $"{key.SanitizePath()}#{SanitizeIsoFilename(filename)}";
            return Path.Combine(uploadOptions.IsoRoot, fileName);
        }
    }

    private string BuildTempPath(string filename, string key)
    {
        if (!Directory.Exists(uploadOptions.TempRoot))
            Directory.CreateDirectory(uploadOptions.TempRoot);

        if (uploadOptions.SupportsSubfolders)
        {
            string path = Path.Combine(
                uploadOptions.TempRoot,
                key.SanitizePath(),
                Actor.Id
            );

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return Path.Combine(
                path,
                SanitizeIsoFilename(filename)
            );
        }
        else
        {
            var fileName = $"{key.SanitizePath()}#{Actor.Id}#{SanitizeIsoFilename(filename)}";
            return Path.Combine(uploadOptions.TempRoot, fileName);
        }
    }

    private string ConvertToDatastorePath(string filename, string key)
    {
        string isoStore = hypervisorService.Options.IsoStore.TrimEnd('/');
        string sanitizedFilename = SanitizeIsoFilename(filename);

        if (uploadOptions.SupportsSubfolders)
        {
            string sanitizedKey = key.SanitizePath();
            return $"{isoStore}/{sanitizedKey}/{sanitizedFilename}";
        }
        else
        {
            string flatName = $"{key.SanitizePath()}#{sanitizedFilename}";
            return $"{isoStore}/{flatName}";
        }
    }

    private async Task UploadToDatastore(string tempFilePath, string datastorePath)
    {
        string fileToUpload = tempFilePath;

        if (!tempFilePath.ToLower().EndsWith(Meta.IsoFileExtension))
        {
            string isoPath = tempFilePath + Meta.IsoFileExtension;

            CDBuilder builder = new()
            {
                UseJoliet = true,
                VolumeIdentifier = Meta.IsoVolumeId
            };
            builder.AddFile(Path.GetFileName(tempFilePath), tempFilePath);
            builder.Build(isoPath);

            System.IO.File.Delete(tempFilePath);
            fileToUpload = isoPath;
            datastorePath += Meta.IsoFileExtension;
        }

        Log("uploading to datastore", null, datastorePath);
        await hypervisorService.UploadFileToDatastore(datastorePath, fileToUpload);
        Log("upload complete", null, datastorePath);

        System.IO.File.Delete(fileToUpload);
    }

    private void CleanupTempFiles(string tempFilePath)
    {
        if (System.IO.File.Exists(tempFilePath))
            System.IO.File.Delete(tempFilePath);
        if (System.IO.File.Exists(tempFilePath + Meta.IsoFileExtension))
            System.IO.File.Delete(tempFilePath + Meta.IsoFileExtension);
    }

    [HttpGet("api/workspace/{workspaceId}/iso-usage")]
    [SwaggerOperation(OperationId = "GetIsoUsage")]
    [Authorize]
    public async Task<ActionResult<IsoUsageReport>> GetIsoUsage(string workspaceId, [FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return BadRequest("ISO path is required");

        var (actualWorkspaceId, filename, error) = await AuthorizeIsoAccess(workspaceId, path);
        if (error != null) return error;

        var templates = await templateStore.FindByIso(path);
        var activeGamespaces = await gamespaceStore.FindActiveByIso(path);

        return Ok(new IsoUsageReport
        {
            Templates = templates.Select(t => new IsoUsageReport.TemplateReference
            {
                Id = t.Id,
                Name = t.Name,
                WorkspaceName = t.Workspace?.Name ?? "(deleted)"
            }).ToList(),
            ActiveGamespaces = activeGamespaces.Select(g => new IsoUsageReport.GamespaceReference
            {
                Id = g.Id,
                Name = g.Name
            }).ToList()
        });
    }

    [HttpDelete("api/workspace/{workspaceId}/iso")]
    [SwaggerOperation(OperationId = "DeleteWorkspaceIso")]
    [Authorize]
    public async Task<ActionResult<bool>> DeleteWorkspaceIso(string workspaceId, [FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(workspaceId) || string.IsNullOrWhiteSpace(path))
            return BadRequest("Workspace ID and ISO path are required");

        var (actualWorkspaceId, filename, error) = await AuthorizeIsoAccess(workspaceId, path);
        if (error != null) return error;

        try
        {
            Logger.LogInformation("Deleting ISO: workspace={workspaceId}, file={filename}", actualWorkspaceId, filename);

            if (uploadOptions.UseDatastoreApi)
            {
                string datastorePath = ConvertToDatastorePath(filename, actualWorkspaceId);
                await hypervisorService.DeleteFileFromDatastore(datastorePath);
                Logger.LogInformation("Deleted datastore ISO: {datastorePath}", datastorePath);
            }
            else
            {
                string filePath = BuildIsoFilePath(actualWorkspaceId, filename);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    Logger.LogInformation("Deleted local ISO: {filePath}", filePath);
                }
            }

            return Json(true);
        }
        catch (FileNotFoundException)
        {
            return NotFound($"ISO file not found: {path}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Logger.LogError(ex, "ISO delete failed - access denied: workspace={workspaceId}, path={path}", actualWorkspaceId, path);
            return StatusCode(403, "Access denied");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ISO delete failed: workspace={workspaceId}, path={path}", actualWorkspaceId, path);
            return StatusCode(500, $"Failed to delete ISO: {ex.Message}");
        }
    }

    private async Task<(string actualWorkspaceId, string filename, ActionResult error)> AuthorizeIsoAccess(string workspaceId, string path)
    {
        string actualWorkspaceId = workspaceId;
        string filename;

        if (path.Contains('/'))
        {
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return (null, null, BadRequest("Invalid ISO path format"));
            actualWorkspaceId = parts[0];
            filename = parts[1];
        }
        else
        {
            filename = path;
        }

        if (actualWorkspaceId != Guid.Empty.ToString())
        {
            if (!await workspaceService.CanEdit(actualWorkspaceId, Actor.Id) && !Actor.IsAdmin)
                return (actualWorkspaceId, filename, Forbid());
        }
        else
        {
            if (!Actor.IsAdmin)
                return (actualWorkspaceId, filename, Forbid());
        }

        return (actualWorkspaceId, filename, null);
    }

    private static string SanitizeIsoFilename(string filename)
        => filename.Replace(" ", "").SanitizeFilename();

    private string BuildIsoFilePath(string workspaceKey, string filename)
    {
        string sanitizedFilename = SanitizeIsoFilename(filename);

        if (uploadOptions.SupportsSubfolders)
        {
            return Path.Combine(
                uploadOptions.IsoRoot,
                workspaceKey.SanitizePath(),
                sanitizedFilename
            );
        }
        else
        {
            string flatName = $"{workspaceKey.SanitizePath()}#{sanitizedFilename}";
            return Path.Combine(uploadOptions.IsoRoot, flatName);
        }
    }

}
