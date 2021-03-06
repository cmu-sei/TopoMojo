// Copyright 2020 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscUtils.Iso9660;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TopoMojo.Abstractions;
using TopoMojo.Extensions;
using TopoMojo.Services;
using TopoMojo.Web.Services;

namespace TopoMojo.Web.Controllers
{
    [Authorize]
    [ApiController]
    public class FileController : _Controller
    {
        public FileController(
            ILogger<AdminController> logger,
            IIdentityResolver identityResolver,
            IFileUploadHandler uploader,
            IFileUploadMonitor monitor,
            FileUploadOptions uploadOptions,
            WorkspaceService workspaceService
        ) : base(logger, identityResolver)
        {
            _monitor = monitor;
            _config = uploadOptions;
            _workspaceService = workspaceService;
            _uploader = uploader;
        }

        private readonly IFileUploadMonitor _monitor;
        private readonly FileUploadOptions _config;
        private readonly WorkspaceService _workspaceService;
        private readonly IFileUploadHandler _uploader;

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
        [ProducesResponseType(typeof(int), 200)]
        public IActionResult Progress(string id)
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
        [DisableFormValueModelBinding]
        [DisableRequestSizeLimit]
        public async Task<ActionResult<bool>> Upload()
        {
            await _uploader.Process(
                Request,
                metadata => {
                    string publicTarget = Guid.Empty.ToString();
                    string original = metadata["original-name"];
                    string filename = metadata["name"] ?? original;
                    string key = metadata["group-key"] ?? publicTarget;
                    long size = Int64.Parse(metadata["size"] ?? "0");

                    if (_config.MaxFileBytes > 0 && size > _config.MaxFileBytes)
                        throw new Exception($"File {filename} exceeds the {_config.MaxFileBytes} byte maximum size.");

                    if (key != publicTarget && !_workspaceService.CanEdit(key).Result)
                        throw new InvalidOperationException();

                    // Log("uploading", null, filename);
                    string dest = BuildDestinationPath(filename, key);
                    metadata.Add("destination-path", dest);
                    Log("uploading", null, dest);

                    return System.IO.File.Create(dest);
                },
                status => {
                    if (status.Error != null)
                    {
                        string dp = status.Metadata["destination-path"];
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
                    string dp = metadata["destination-path"];

                    if (!dp.ToLower().EndsWith(".iso") && System.IO.File.Exists(dp))
                    {
                        CDBuilder builder = new CDBuilder();
                        builder.UseJoliet = true;
                        builder.VolumeIdentifier = "UploadedFile";
                        builder.AddFile(Path.GetFileName(dp), dp);
                        builder.Build(dp + ".iso");
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
