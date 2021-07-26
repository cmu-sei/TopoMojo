// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using DiscUtils.Iso9660;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Services;
using TopoMojo.Api.Hubs;
using Swashbuckle.AspNetCore.Annotations;

namespace TopoMojo.Api.Controllers
{
    [Authorize]
    [ApiController]
    public class FileController : _Controller
    {
        public FileController(
            ILogger<AdminController> logger,
            IHubContext<AppHub, IHubEvent> hub,
            IFileUploadHandler uploader,
            IFileUploadMonitor monitor,
            FileUploadOptions uploadOptions,
            WorkspaceService workspaceService
        ) : base(logger, hub)
        {
            _monitor = monitor;
            _config = uploadOptions;
            _svc = workspaceService;
            _uploader = uploader;
        }

        private readonly IFileUploadMonitor _monitor;
        private readonly FileUploadOptions _config;
        private readonly WorkspaceService _svc;
        private readonly IFileUploadHandler _uploader;
        private static class Meta
        {
            public const string OriginalName = "original-name";
            public const string Name = "name";
            public const string GroupKey = "group-key";
            public const string Size = "size";
            public const string DestinationPath = "destination-path";
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
            return Json(_monitor.Check(id).Progress);
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
            await _uploader.Process(
                Request,
                metadata => {
                    string publicTarget = Guid.Empty.ToString();
                    string original = metadata[Meta.OriginalName];
                    string filename = metadata[Meta.Name] ?? original;
                    string key = metadata[Meta.GroupKey] ?? publicTarget;
                    long size = Int64.Parse(metadata[Meta.Size] ?? "0");

                    if (_config.MaxFileBytes > 0 && size > _config.MaxFileBytes)
                        throw new Exception($"File {filename} exceeds the {_config.MaxFileBytes} byte maximum size.");

                    if (key != publicTarget && !_svc.CanEdit(key, Actor.Id).Result && !Actor.IsAdmin)
                        throw new InvalidOperationException();

                    // Log("uploading", null, filename);
                    string dest = BuildDestinationPath(filename, key);
                    metadata.Add(Meta.DestinationPath, dest);
                    Log("uploading", null, dest);

                    return System.IO.File.Create(dest);
                },
                status => {
                    if (status.Error != null)
                    {
                        string dp = status.Metadata[Meta.DestinationPath];
                        if (System.IO.File.Exists(dp))
                            System.IO.File.Delete(dp);
                    }
                    _monitor.Update(status.Key, status.Progress);
                    // TODO: broadcast progress to group
                },

                options => {
                    options.MultipartBodyLengthLimit = (long)((_config.MaxFileBytes > 0) ? _config.MaxFileBytes : 1E9);
                },

                metadata => {
                    string dp = metadata[Meta.DestinationPath];

                    if (!dp.ToLower().EndsWith(Meta.IsoFileExtension) && System.IO.File.Exists(dp))
                    {
                        CDBuilder builder = new CDBuilder();
                        builder.UseJoliet = true;
                        builder.VolumeIdentifier = Meta.IsoVolumeId;
                        builder.AddFile(Path.GetFileName(dp), dp);
                        builder.Build(dp + Meta.IsoFileExtension);
                        System.IO.File.Delete(dp);
                    }
                }
            );

            return Json(true);
        }

        private string BuildDestinationPath(string filename, string key)
        {
            string path = Path.Combine(
                _config.IsoRoot,
                key.SanitizePath()
            );

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return Path.Combine(
                path,
                filename.Replace(" ", "").SanitizeFilename()
            );
        }

    }

}
