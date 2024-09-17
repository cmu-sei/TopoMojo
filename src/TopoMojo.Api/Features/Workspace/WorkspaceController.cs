// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Hubs;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Api.Validators;

namespace TopoMojo.Api.Controllers
{
    [Authorize]
    [ApiController]
    [TypeFilter(typeof(WorkspaceValidator))]
    public class WorkspaceController(
        ILogger<WorkspaceController> logger,
        IHubContext<AppHub, IHubEvent> hub,
        IHypervisorService podService,
        WorkspaceService workspaceService
        ) : _Controller(logger, hub)
    {

        /// <summary>
        /// List workspaces according to search parameters.
        /// </summary>
        /// <remarks>
        /// ?aud=value retrieves item published to that audience,
        /// if the requestor is allowed that scope
        /// filter=my retrieves where actor is worker
        /// doc=1 includes "above the cut" portion of workspace document markdown
        /// doc=2 includes full workspace document markdown
        /// sort: age for newest first; default is alpha
        /// </remarks>
        /// <param name="search"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [Authorize]
        [HttpGet("api/workspaces")]
        [SwaggerOperation(OperationId = "ListWorkspaces")]
        public async Task<ActionResult<WorkspaceSummary[]>> ListWorkspaces([FromQuery]WorkspaceSearch search, CancellationToken ct)
        {
            if (search.WantsAudience && !Actor.HasScope(search.aud))
            {
                ModelState.AddModelError("search", "ActorLacksAudienceScope");
                return ValidationProblem();
            }

            if (!AuthorizeAll()) return Forbid();

            search.scope = Actor.Scope;

            return Ok(
                await workspaceService.List(search, Actor.Id, Actor.IsAdmin, ct)
            );
        }

        /// <summary>
        /// Load a workspace.
        /// </summary>
        /// <param name="id">Workspace Id</param>
        /// <returns></returns>
        [HttpGet("api/workspace/{id}")]
        [SwaggerOperation(OperationId = "LoadWorkspace")]
        [Authorize]
        public async Task<ActionResult<Workspace>> LoadWorkspace(string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(id, Actor.Id).Result
            )) return Forbid();

            return Ok(
                await workspaceService.Load(id)
            );
        }

        /// <summary>
        /// Create a new workspace.
        /// </summary>
        /// <param name="model">New Workspace</param>
        /// <returns>A new workspace.</returns>
        [HttpPost("api/workspace")]
        [SwaggerOperation(OperationId = "CreateWorkspace")]
        [Authorize]
        public async Task<ActionResult<Workspace>> CreateWorkspace([FromBody]NewWorkspace model)
        {
            if (!AuthorizeAny(
                () => Actor.IsCreator,
                () => workspaceService.CheckWorkspaceLimit(Actor.Id).Result
            )) return Forbid();

            return Ok(
                await workspaceService.Create(model, Actor.Id, Actor.Name, Actor.IsCreator)
            );
        }

        /// <summary>
        /// Clone a workspace.
        /// </summary>
        /// <param name="id">Workspace Id to clone</param>
        /// <returns>A new workspace.</returns>
        [HttpPost("api/workspace/{id}/clone")]
        [SwaggerOperation(OperationId = "CloneWorkspace")]
        [Authorize]
        public async Task<ActionResult<Workspace>> CloneWorkspace([FromRoute]string id)
        {
            if (!AuthorizeAny(
                () => Actor.IsCreator,
                () => workspaceService.CheckWorkspaceLimit(Actor.Id).Result
            )) return Forbid();

            return Ok(
                await workspaceService.Clone(id)
            );
        }

        /// <summary>
        /// Update an existing workspace.
        /// </summary>
        /// <param name="model">Changed Workspace</param>
        /// <returns></returns>
        [HttpPut("api/workspace")]
        [SwaggerOperation(OperationId = "UpdateWorkspace")]
        [Authorize]
        public async Task<ActionResult> UpdateWorkspace([FromBody]RestrictedChangedWorkspace model)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(model.Id, Actor.Id).Result
            )) return Forbid();

            Workspace workspace = await workspaceService.Update(model);

            await Hub.Clients
                .Group(workspace.Id)
                .TopoEvent(new BroadcastEvent<Workspace>(User, "TOPO.UPDATED", workspace))
            ;

            return Ok();
        }

        /// <summary>
        /// Update an existing workspace.
        /// </summary>
        /// <param name="model">Changed Workspace (privileged</param>
        /// <returns></returns>
        [HttpPut("api/workspace/priv")]
        [SwaggerOperation(OperationId = "PrivilegedUpdateWorkspace")]
        [Authorize]
        public async Task<ActionResult> PrivilegedUpdateWorkspace([FromBody]ChangedWorkspace model)
        {
            if (!AuthorizeAny(
                () => Actor.IsAdmin
            )) return Forbid();

            Workspace workspace = await workspaceService.Update(model);

            await Hub.Clients
                .Group(workspace.Id)
                .TopoEvent(new BroadcastEvent<Workspace>(User, "TOPO.UPDATED", workspace))
            ;

            return Ok();
        }

        /// <summary>
        /// Delete a workspace.
        /// </summary>
        /// <param name="id">Workspace Id</param>
        /// <returns></returns>
        [HttpDelete("api/workspace/{id}")]
        [SwaggerOperation(OperationId = "DeleteWorkspace")]
        [Authorize]
        public async Task<ActionResult> DeleteWorkspace(string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanManage(id, Actor.Id).Result
            )) return Forbid();

            var workspace = await workspaceService.Delete(id);

            Log("deleted", workspace);

            await Hub.Clients
                .Group(workspace.Id)
                .TopoEvent(new BroadcastEvent<Workspace>(User, "TOPO.DELETED", workspace));

            return Ok();
        }

        /// <summary>
        /// Find ISO files available to a workspace.
        /// </summary>
        /// <param name="id">Workspace Id</param>
        /// <returns></returns>
        [HttpGet("api/workspace/{id}/isos")]
        [SwaggerOperation(OperationId = "LoadWorkspaceIsos")]
        [Authorize]
        public async Task<ActionResult<VmOptions>> LoadWorkspaceIsos(string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(id, Actor.Id).Result
            )) return Forbid();

            return Ok(
                await podService.GetVmIsoOptions(id)
            );
        }

        /// <summary>
        /// Find virtual networks available to a workspace.
        /// </summary>
        /// <param name="id">Workspace Id</param>
        /// <returns></returns>
        [HttpGet("api/workspace/{id}/nets")]
        [SwaggerOperation(OperationId = "LoadWorkspaceNets")]
        [Authorize]
        public async Task<ActionResult<VmOptions>> LoadWorkspaceNets(string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(id, Actor.Id).Result
            )) return Forbid();

            return Ok(
                await podService.GetVmNetOptions(id)
            );
        }

        /// <summary>
        /// Load gamespaces generated from a workspace.
        /// </summary>
        /// <param name="id">Workspace Id</param>
        /// <returns></returns>
        [HttpGet("api/workspace/{id}/stats")]
        [SwaggerOperation(OperationId = "GetWorkspaceStats")]
        [Authorize]
        public async Task<ActionResult<WorkspaceStats>> GetWorkspaceStats(string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(id, Actor.Id).Result
            )) return Forbid();

            return Ok(
                await workspaceService.GetStats(id)
            );
        }

        /// <summary>
        /// Load templates availabe to workspace.
        /// </summary>
        /// <param name="id">Workspace Id</param>
        /// <returns></returns>
        [HttpGet("api/workspace/{id}/templates")]
        [SwaggerOperation(OperationId = "LoadWorkspaceTemplates")]
        [Authorize]
        public async Task<ActionResult<TemplateSummary[]>> LoadWorkspaceTemplates(string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(id, Actor.Id).Result
            )) return Forbid();

            return Ok(
                await workspaceService.GetScopedTemplates(id, Actor.Scope)
            );
        }

        /// <summary>
        /// Delete all gamespaces generated from this workspace.
        /// </summary>
        /// <remarks>
        /// Useful if updating a workspace after it is published.
        /// Workspace updates are disallowed if gamespaces exist.
        /// </remarks>
        /// <param name="id">Workspace Id</param>
        /// <returns></returns>
        [HttpDelete("api/workspace/{id}/games")]
        [SwaggerOperation(OperationId = "DeleteWorkspaceGames")]
        [Authorize]
        public async Task<ActionResult<WorkspaceStats>> DeleteWorkspaceGames(string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(id, Actor.Id).Result
            )) return Forbid();

            var games = await workspaceService.KillGames(id);

            List<Task> tasklist = [];

            foreach (var game in games)
                tasklist.Add(
                    Hub.Clients
                        .Group(game.Id)
                        .GameEvent(new BroadcastEvent<GameState>(User, "GAME.OVER", game))
                );

            await Task.WhenAll([.. tasklist]);

            return Ok(
                await GetWorkspaceStats(id)
            );
        }

        /// <summary>
        /// Generate an invitation code for worker enlistment.
        /// </summary>
        /// <param name="id">Workspace Id</param>
        /// <returns></returns>
        [HttpPut("api/workspace/{id}/invite")]
        [SwaggerOperation(OperationId = "GetWorkspaceInvite")]
        [Authorize]
        public async Task<ActionResult<JoinCode>> GetWorkspaceInvite(string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanManage(id, Actor.Id).Result
            )) return Forbid();

            return Ok(
                await workspaceService.Invite(id)
            );
        }

        /// <summary>
        /// Get a workspace's challenge spec
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("api/challenge/{id}")]
        [SwaggerOperation(OperationId = "GetChallengeSpec")]
        [Authorize]
        public async Task<ActionResult<ChallengeSpec>> GetChallengeSpec([FromRoute] string id)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(id, Actor.Id).Result
            )) return Forbid();

            return Ok(
                await workspaceService.GetChallenge(id)
            );
        }

        /// <summary>
        /// Update a workspace's challenge spec
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("api/challenge/{id}")]
        [SwaggerOperation(OperationId = "UpdateChallengeSpec")]
        [Authorize]
        public async Task<IActionResult> UpdateChallengeSpec([FromRoute]string id, [FromBody] ChallengeSpec model)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanEdit(id, Actor.Id).Result
            )) return Forbid();

            await workspaceService.UpdateChallenge(id, model);

            // TODO: broadcast updated

            return Ok();
        }

        /// <summary>
        /// Accept an invitation to a workspace.
        /// </summary>
        /// <remarks>
        /// Any user that submits the invitation code is
        /// added as member of the workspace.
        /// </remarks>
        /// <param name="code">Invitation Code</param>
        /// <returns></returns>
        [HttpPost("api/worker/{code}")]
        [ApiExplorerSettings(IgnoreApi = true)]
        [Authorize]
        public async Task<ActionResult<WorkspaceSummary>> BecomeWorker(string code)
        {
            return Ok(
                await workspaceService.Enlist(code, Actor.Id, Actor.Name)
            );
        }

        /// <summary>
        /// Removes a worker from the workspace.
        /// </summary>
        /// <param name="id">Workspace Id</param>
        /// <param name="sid">Subject Id of target member</param>
        /// <returns></returns>
        [HttpDelete("api/workspace/{id}/worker/{sid}")]
        [SwaggerOperation(OperationId = "RemoveWorker")]
        [Authorize]
        public async Task<ActionResult> RemoveWorker([FromRoute] string id, string sid)
        {
            if (!AuthorizeAny(
                () => workspaceService.CanManage(id, Actor.Id).Result
            )) return Forbid();

            await workspaceService.Delist(id, sid, Actor.IsAdmin);

            return Ok();
        }


    }

}
