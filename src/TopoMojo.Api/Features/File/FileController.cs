// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using DiscUtils.Iso9660;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    TopoMojo.Hypervisor.IHypervisorService hypervisorService
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
                    long fileSize = long.Parse(metadata[Meta.Size] ?? "0");
                    bool isLargeFile = fileSize > uploadOptions.AsyncUploadThresholdBytes;

                    if (isLargeFile)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await UploadToDatastore(dp, datastorePath);
                            }
                            catch (Exception ex)
                            {
                                Log("async upload failed", null, ex.Message);
                            }
                        });
                        return;
                    }
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
                filename.Replace(" ", "").SanitizeFilename()
            );
        }
        else
        {
            var fileName = $"{key.SanitizePath()}#{filename.Replace(" ", "").SanitizeFilename()}";
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
                filename.Replace(" ", "").SanitizeFilename()
            );
        }
        else
        {
            var fileName = $"{key.SanitizePath()}#{Actor.Id}#{filename.Replace(" ", "").SanitizeFilename()}";
            return Path.Combine(uploadOptions.TempRoot, fileName);
        }
    }

    private string ConvertToDatastorePath(string filename, string key)
    {
        string isoStore = hypervisorService.Options.IsoStore.TrimEnd('/');
        string sanitizedFilename = filename.Replace(" ", "").SanitizeFilename();

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

}
