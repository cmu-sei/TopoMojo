// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;

namespace TopoMojo.Api.Controllers;

[Authorize]
[ApiController]
public class DocumentController(
    ILogger<AdminController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    WorkspaceService workspaceService,
    FileUploadOptions uploadOptions
    ) : _Controller(logger, hub)
{

    /// <summary>
    /// Load workspace document
    /// </summary>
    /// <param name="id">Workspace Id</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns></returns>
    [HttpGet("api/document/{id}")]
    [SwaggerOperation(OperationId = "LoadDocument")]
    [Authorize]
    public async Task<ActionResult<string>> LoadDocument(string id, CancellationToken ct)
    {

        if (!AuthorizeAny(
            () => workspaceService.CanEdit(id, Actor.Id).Result
        )) return Forbid();

        string path = BuildPath();

        path = System.IO.Path.Combine(path, id + ".md");

        if (!System.IO.File.Exists(path))
            return Ok("");

        string result = await System.IO.File.ReadAllTextAsync(path, ct);

        if (string.IsNullOrEmpty(result))
            return Ok("");

        // string token = new Random().Next().ToString("x");
        // string find = "(\\[.*\\]\\([^)]*)";
        // string replace = $"$1?t={token}";

        // result = Regex.Replace(result, "\\?t=[^)]*", "");
        // result = Regex.Replace(result, find, replace);
        // // add token/id to cache for 30s

        return Ok(result);
    }

    /// <summary>
    /// Save markdown as document.
    /// </summary>
    /// <param name="id">Workspace Id</param>
    /// <param name="text">Markdown text</param>
    /// <returns></returns>
    [HttpPut("api/document/{id}")]
    [SwaggerOperation(OperationId = "SaveDocument")]
    [Authorize]
    public async Task<ActionResult> SaveDocument(string id, [FromBody]string text)
    {
        if (!AuthorizeAny(
            () => workspaceService.CanEdit(id, Actor.Id).Result
        )) return Forbid();

        string path = BuildPath();

        path = System.IO.Path.Combine(path, id + ".md");
        await System.IO.File.WriteAllTextAsync(path, text);

        SendBroadcast(id, "saved", text);

        return Ok();
    }

    /// <summary>
    /// List document image files.
    /// </summary>
    /// <param name="id">Workspace Id</param>
    /// <returns></returns>
    [HttpGet("api/images/{id}")]
    [SwaggerOperation(OperationId = "ListDocumentImages")]
    [Authorize]
    public ActionResult<ImageFile[]> ListDocumentImages(string id)
    {
        if (!AuthorizeAny(
            () => workspaceService.CanEdit(id, Actor.Id).Result
        )) return Forbid();

        string path = Path.Combine(uploadOptions.DocRoot, id);

        if (!Directory.Exists(path))
            return Ok(new ImageFile[]{});

        return Ok(
            Directory.GetFiles(path)
            .Select(x => new ImageFile { Filename = Path.GetFileName(x)})
            .ToArray()
        );
    }

    /// <summary>
    /// Delete document image file.
    /// </summary>
    /// <param name="id">Workspace Id</param>
    /// <param name="filename"></param>
    /// <returns></returns>
    [HttpDelete("api/image/{id}")]
    [SwaggerOperation(OperationId = "DeleteDocumentImage")]
    [Authorize]
    public IActionResult DeleteDocumentImage(string id, string filename)
    {
        if (!AuthorizeAny(
            () => workspaceService.CanEdit(id, Actor.Id).Result
        )) return Forbid();

        string path = BuildPath(id, filename);

        if (filename.IsEmpty() || !System.IO.File.Exists(path))
            return BadRequest();

        System.IO.File.Delete(path);
        return Ok();
    }

    /// <summary>
    /// Upload document image file.
    /// </summary>
    /// <param name="id">Workspace Id</param>
    /// <param name="file"></param>
    /// <returns></returns>
    [HttpPost("api/image/{id}")]
    [SwaggerOperation(OperationId = "UploadDocument")]
    [Authorize]
    public async Task<ActionResult<ImageFile>> UploadDocument(string id, IFormFile file)
    {
        if (!AuthorizeAny(
            () => workspaceService.CanEdit(id, Actor.Id).Result
        )) return Forbid();

        string path = BuildPath(id);

        string filename = file.FileName.SanitizeFilename();

        if (filename.Length > 50)
            filename = filename.Substring(0, 50);

        string ext = Path.GetExtension(filename);

        filename = filename.Replace(
            ext,
            $"-{new Random().Next()}{ext}"
        );

        path = Path.Combine(path, filename);

        using (var stream = new FileStream(path, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new ImageFile { Filename = filename});
    }

    private string BuildPath(params string[] segments)
    {
        string path = uploadOptions.DocRoot;

        foreach (string s in segments)
            path = System.IO.Path.Combine(path, s);

        if (!System.IO.Directory.Exists(path) && !System.IO.File.Exists(path))
            System.IO.Directory.CreateDirectory(path);

        return path;
    }

    private void SendBroadcast(string roomId, string action, string text)
    {
        Hub.Clients
            .Group(roomId)
            .DocumentEvent(
                new BroadcastEvent<Document>(
                    User,
                    "DOCUMENT." + action.ToUpper(),
                    new Document {
                        Text = text,
                        Timestamp = (long)DateTimeOffset.UtcNow.Subtract(DateTimeOffset.UnixEpoch).TotalMilliseconds // DateTimeOffset.UtcNow.ToString("ss.ffff")
                    }
                )
            );
    }

}
