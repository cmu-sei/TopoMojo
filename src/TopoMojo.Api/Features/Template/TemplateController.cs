// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Api.Validators;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Controllers
{
    [ApiController]
    [Authorize]
    public class TemplateController : _Controller
    {
        public TemplateController(
            ILogger<AdminController> logger,
            IHubContext<AppHub, IHubEvent> hub,
            TemplateValidator validator,
            TemplateService templateService,
            IHypervisorService podService
        ) : base(logger, hub, validator)
        {
            _svc = templateService;
            _pod = podService;
        }

        private readonly TemplateService _svc;
        private readonly IHypervisorService _pod;

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
            await Validate(search);

            AuthorizeAll();

            var result = await _svc.List(search, Actor.IsAdmin, ct);

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
            await Validate(new Entity { Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanEdit(id, Actor.Id).Result
            );

            return Ok(
                await _svc.Load(id)
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
            await Validate(model);

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanEdit(model.Id, Actor.Id).Result
            );

            var result = await _svc.Update(model);

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
            await Validate(new Entity { Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanEdit(id, Actor.Id).Result
            );

            var result = await _svc.Delete(id);

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
            await Validate(model);

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanEditWorkspace(model.WorkspaceId, Actor.Id).Result
            );

            var result = await _svc.Link(model, Actor.IsCreator);

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
            await Validate(model);

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanEdit(model.TemplateId, Actor.Id).Result
            );

            var result = await _svc.Unlink(model);

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
            await Validate(new Entity{ Id = id });

            AuthorizeAll();

            return Ok(
                await _svc.LoadDetail(id)
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
            await Validate(model);

            AuthorizeAll();

            return Ok(
                await _svc.Create(model)
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
        public async Task<ActionResult> ConfigureTemplateDetail([FromBody]TemplateDetail model)
        {
            await Validate(model);

            AuthorizeAll();

            var result = await _svc.Configure(model);

            return Ok();
        }

        [HttpGet("api/report/disks")]
        [SwaggerOperation(OperationId = "AttachedDisksReport")]
        [Authorize(AppConstants.AdminOnlyPolicy)]
        public async Task<ActionResult> AttachedDisksReport()
        {
            return Ok(
                await _svc.DiskReport()
            );
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
}
