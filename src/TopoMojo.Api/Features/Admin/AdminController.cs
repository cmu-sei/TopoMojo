// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Features.Theme;
using TopoMojo.Api.Services;

namespace TopoMojo.Api.Controllers;

[Authorize(AppConstants.AdminOnlyPolicy)]
[ApiController]
public class AdminController(
    ILogger<AdminController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    TransferService transferSvc,
    FileUploadOptions fileUploadOptions,
    JanitorService janitor,
    HubCache hubCache,
    IMemoryCache localCache,
    IWebHostEnvironment env
    ) : BaseController(logger, hub)
{

    /// <summary>
    /// Post an announcement to users.
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    [HttpPost("api/admin/announce")]
    [SwaggerOperation(OperationId = "PostAnnouncement")]
    public async Task<ActionResult<bool>> PostAnnouncement([FromBody] string text)
    {
        await Task.Run(() => SendBroadcast(text));
        return Ok(true);
    }

    /// <summary>
    /// Generate an export package.
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [HttpPost("api/admin/export")]
    [ProducesResponseType(typeof(string[]), 200)]
    [SwaggerOperation(OperationId = "ExportWorkspaces")]
    public async Task<ActionResult> ExportWorkspaces([FromBody] string[] ids)
    {
        await transferSvc.Export(
            ids,
            fileUploadOptions.DocRoot,
            Path.Combine(fileUploadOptions.TopoRoot, "_export")
        );

        return Ok();
    }

    /// <summary>
    /// Initiate import process.
    /// </summary>
    /// <returns></returns>
    [HttpGet("api/admin/import")]
    [SwaggerOperation(OperationId = "ImportWorkspaces")]
    public async Task<ActionResult<string[]>> ImportWorkspaces()
    {
        return Ok(await transferSvc.Import(
            fileUploadOptions.TopoRoot,
            fileUploadOptions.DocRoot
        ));
    }

    /// <summary>
    /// Generate an download package.
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    [HttpPost("api/admin/download")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [SwaggerOperation(OperationId = "DownloadWorkspaces")]
    public async Task<ActionResult> DownloadWorkspaces([FromBody] string[] ids)
    {
        var stream = await transferSvc.Download(ids, fileUploadOptions.DocRoot);
        return File(stream, "application/zip", "topomojo-export.zip");
    }

    /// <summary>
    /// Initiate upload process.
    /// </summary>
    /// <returns></returns>
    [HttpPost("api/admin/upload")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = int.MaxValue, ValueLengthLimit = int.MaxValue, ValueCountLimit = int.MaxValue)]
    [SwaggerOperation(OperationId = "UploadWorkspaces")]
    public async Task<ActionResult<string[]>> UploadWorkspaces([FromForm] List<IFormFile> files)
    {
        return Ok(await transferSvc.Upload(files, fileUploadOptions.DocRoot));
    }

    /// <summary>
    /// Show online users.
    /// </summary>
    /// <returns></returns>
    [HttpGet("api/admin/live")]
    [SwaggerOperation(OperationId = "ListActiveUsers")]
    public ActionResult<CachedConnection[]> ListActiveUsers()
    {
        return Ok(hubCache.Connections.Values);
    }

    /// <summary>
    /// Run clean up tasks
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    [HttpPost("api/admin/janitor")]
    [SwaggerOperation(OperationId = "RunJanitorCleanup")]
    public async Task<ActionResult<JanitorReport[]>> RunJanitorCleanup([FromBody] JanitorOptions options = null)
    {
        return Ok(await janitor.Cleanup(options));
    }

    [HttpPost("api/admin/janitor/idlewsvms")]
    [SwaggerOperation(OperationId = "RunJanitorCleanupIdleWorkspaceVms")]
    public async Task<ActionResult<JanitorReport[]>> RunJanitorCleanupIdleWorkspaceVms([FromBody] JanitorOptions options = null)
    {
        return Ok(await janitor.CleanupIdleWorkspaceVms(options));
    }

    [HttpGet("api/admin/log")]
    [SwaggerOperation(OperationId = "GetAdminLog")]
    public ActionResult<TimestampedException[]> GetAdminLog([FromQuery] string since)
    {
        var errbf = localCache.Get<List<TimestampedException>>(AppConstants.ErrorListCacheKey)
            ?? [];

        if (!DateTimeOffset.TryParse(since, out DateTimeOffset ts))
            ts = DateTimeOffset.MinValue;

        return Ok(
            errbf.Where(e => e.Timestamp > ts).ToArray()
        );
    }

    private void SendBroadcast(string text = "")
    {
        Hub.Clients.All.GlobalEvent(
                new BroadcastEvent<string>(
                    User,
                    "GLOBAL.ANNOUNCE",
                    text
                )
            );
    }

    public class BackgroundUploadRequest
    {
        public IFormFile File { get; set; } = default!;
    }


    [HttpPost("api/admin/background")]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [SwaggerOperation(OperationId = "UploadBackground")]
    [ProducesResponseType(typeof(ThemeInfo), 200)]
    public async Task<ActionResult<ThemeInfo>> UploadBackground([FromForm] BackgroundUploadRequest request)
    {
        var file = request.File;

        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".png" and not ".jpg" and not ".jpeg" and not ".webp")
            return BadRequest("Only png, jpg/jpeg, webp are allowed.");

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var themeDir = Path.Combine(webRoot, "theme");
        Directory.CreateDirectory(themeDir);

        foreach (var e in new[] { ".png", ".jpg", ".jpeg", ".webp" })
        {
            var p = Path.Combine(themeDir, "background" + e);
            if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
        }

        var savePath = Path.Combine(themeDir, "background" + ext);

        await using (var fs = System.IO.File.Create(savePath))
        {
            await file.CopyToAsync(fs);
        }

        var ticks = System.IO.File.GetLastWriteTimeUtc(savePath).Ticks;
        var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/theme/background?v={ticks}";
        return Ok(new ThemeInfo { BackgroundUrl = url });
    }


    [HttpDelete("api/admin/background")]
    public ActionResult<ThemeInfo> ClearBackground()
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var themeDir = Path.Combine(webRoot, "theme");

        if (Directory.Exists(themeDir))
        {
            foreach (var e in new[] { ".png", ".jpg", ".jpeg", ".webp" })
            {
                var p = Path.Combine(themeDir, "background" + e);
                if (System.IO.File.Exists(p)) System.IO.File.Delete(p);
            }
        }

        return Ok(new ThemeInfo { BackgroundUrl = null });
    }

}
