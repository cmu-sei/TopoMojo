// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Api.Validators;

namespace TopoMojo.Api.Controllers;

[ApiController]
[Authorize]
[TypeFilter<TemplateValidator>]
public class TemplateController(
    ILogger<AdminController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    TemplateService templateService
    ) : _Controller(logger, hub)
{

    /// <summary>
    /// List templates.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="ct"></param>
    /// <remarks>
    /// Filter for `published`.
    /// Non-Admins always get `published`.
    /// Admins can use `pid=id` for parents (id = 0) or children (id > 0)
    /// </remarks>
    /// <returns>TemplateSummary[]</returns>
    [HttpGet("api/templates")]
    [SwaggerOperation(OperationId = "ListTemplates")]
    [Authorize]
    public async Task<ActionResult<TemplateSummary[]>> ListTemplates([FromQuery]TemplateSearch search, CancellationToken ct)
    {
        if (!AuthorizeAll()) return Forbid();

        var result = await templateService.List(search, Actor.IsAdmin, ct);

        return Ok(result);
    }

    [HttpGet("api/template/{id}/siblings")]
    [SwaggerOperation(OperationId = "ListSiblings")]
    [Authorize]
    public async Task<ActionResult<TemplateSummary[]>> ListSiblings(string id, [FromQuery] bool pub)
    {
        if (!AuthorizeAny(
            () => templateService.CanEdit(id, Actor.Id).Result
        )) return Forbid();

        var result = await templateService.ListSiblings(id, pub);

        return Ok(result);
    }

    /// <summary>
    /// Load a template.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("api/template/{id}")]
    [SwaggerOperation(OperationId = "LoadTemplate")]
    [Authorize]
    public async Task<ActionResult<Template>> LoadTemplate(string id)
    {
        if (!AuthorizeAny(
            () => templateService.CanEdit(id, Actor.Id).Result
        )) return Forbid();

        return Ok(
            await templateService.Load(id)
        );
    }

    /// <summary>
    /// Update a template.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("api/template")]
    [SwaggerOperation(OperationId = "UpdateTemplate")]
    [Authorize]
    public async Task<ActionResult> UpdateTemplate([FromBody]ChangedTemplate model)
    {
        if (!AuthorizeAny(
            () => templateService.CanEdit(model.Id, Actor.Id).Result
        )) return Forbid();

        var result = await templateService.Update(model);

        SendBroadcast(result, "updated");

        return Ok();
    }

    /// <summary>
    /// Delete a template.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("api/template/{id}")]
    [SwaggerOperation(OperationId = "DeleteTemplate")]
    [Authorize]
    public async Task<ActionResult> DeleteTemplate(string id)
    {
        if (!AuthorizeAny(
            () => templateService.CanEdit(id, Actor.Id).Result
        )) return Forbid();

        var result = await templateService.Delete(id);

        SendBroadcast(result, "removed");

        return Ok();
    }

    /// <summary>
    /// Create a new template linked to a parent template.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("api/template")]
    [SwaggerOperation(OperationId = "LinkTemplate")]
    [Authorize]
    public async Task<ActionResult<Template>> LinkTemplate([FromBody]TemplateLink model)
    {
        if (!AuthorizeAny(
            () => templateService.HasValidAudience(model.TemplateId, model.WorkspaceId, Actor.Scope).Result
        )) return Forbid();

        var result = await templateService.Link(model, Actor.IsCreator);

        SendBroadcast(result, "added");

        return Ok(result);
    }

    /// <summary>
    /// Detach a template from its parent.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("api/template/unlink")]
    [SwaggerOperation(OperationId = "UnLinkTemplate")]
    [Authorize]
    public async Task<ActionResult<Template>> UnLinkTemplate([FromBody]TemplateLink model)
    {
        if (!AuthorizeAny(
            () => templateService.CanEdit(model.TemplateId, Actor.Id).Result
        )) return Forbid();

        var result = await templateService.Unlink(model);

        SendBroadcast(result, "updated");

        return Ok(result);
    }

    /// <summary>
    /// Change a template's parent.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("api/template/relink")]
    [SwaggerOperation(OperationId = "ReLinkTemplate")]
    [Authorize]
    public async Task<ActionResult<Template>> ReLinkTemplate([FromBody]TemplateReLink model)
    {
        if (!AuthorizeAny(
            () => templateService.CanEdit(model.TemplateId, Actor.Id).Result
        )) return Forbid();

        var result = await templateService.ReLink(model);

        SendBroadcast(result, "updated");

        return Ok(result);
    }

    /// <summary>
    /// Load template detail.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("api/template-detail/{id}")]
    [SwaggerOperation(OperationId = "LoadTemplateDetail")]
    [Authorize(AppConstants.AdminOnlyPolicy)]
    public async Task<ActionResult<TemplateDetail>> LoadTemplateDetail(string id)
    {
        if (!AuthorizeAll()) return Forbid();

        return Ok(
            await templateService.LoadDetail(id)
        );
    }

    /// <summary>
    /// Create new template with detail.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("api/template-detail")]
    [SwaggerOperation(OperationId = "CreateTemplateDetail")]
    [Authorize(AppConstants.AdminOnlyPolicy)]
    public async Task<ActionResult<TemplateDetail>> CreateTemplateDetail([FromBody]NewTemplateDetail model)
    {
        if (!AuthorizeAll()) return Forbid();

        return Ok(
            await templateService.Create(model)
        );
    }

    /// <summary>
    /// Clone template.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("api/template/clone")]
    [SwaggerOperation(OperationId = "CloneTemplateDetail")]
    [Authorize(AppConstants.AdminOnlyPolicy)]
    public async Task<ActionResult<TemplateDetail>> CloneTemplateDetail([FromBody]TemplateClone model)
    {
        if (!AuthorizeAll()) return Forbid();

        return Ok(
            await templateService.Clone(model)
        );
    }

    /// <summary>
    /// Update template detail.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPut("api/template-detail")]
    [SwaggerOperation(OperationId = "ConfigureTemplateDetail")]
    [Authorize(AppConstants.AdminOnlyPolicy)]
    public async Task<ActionResult> ConfigureTemplateDetail([FromBody]ChangedTemplateDetail model)
    {
        if (!AuthorizeAll()) return Forbid();

        await templateService.Configure(model);

        return Ok();
    }

    [HttpGet("api/report/disks")]
    [SwaggerOperation(OperationId = "AttachedDisksReport")]
    [Authorize(AppConstants.AdminOnlyPolicy)]
    public async Task<ActionResult> AttachedDisksReport()
    {
        return Ok(
            await templateService.DiskReport()
        );
    }

    [HttpGet("api/healthz/{id}")]
    [SwaggerOperation(OperationId = "CheckHealth")]
    [AllowAnonymous]
    public async Task<ActionResult> CheckHealth([FromRoute]string id)
    {
        bool healthy = await templateService.CheckHealth(id);
        if (healthy)
            return Ok();
        return BadRequest();
    }

    private void SendBroadcast(Template template, string action)
    {
        Hub.Clients
            .Group(template.WorkspaceId ?? Guid.Empty.ToString())
            .TemplateEvent(
                new BroadcastEvent<Template>(
                    User,
                    "TEMPLATE." + action.ToUpper(),
                    template
                )
            );
    }
}
