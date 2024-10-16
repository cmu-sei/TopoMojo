// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;

namespace TopoMojo.Api.Controllers;

[Authorize]
[ApiController]
[TypeFilter<GamespaceValidator>]
public class GamespaceController(
    ILogger<AdminController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    CoreOptions options,
    GamespaceService gamespaceService,
    IDistributedCache distributedCache
    ) : _Controller(logger, hub)
{
    private readonly DistributedCacheEntryOptions _cacheOpts = new()
    {
        SlidingExpiration = new TimeSpan(0, 0, 180)
    };

    /// <summary>
    /// List running gamespaces.
    /// </summary>
    /// <remarks>
    /// By default, result is filtered to user's gamespaces.
    /// An administrator can override default filter with filter = "all".
    /// </remarks>
    /// <param name="model">GamespaceSearch query string</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpGet("api/gamespaces")]
    [SwaggerOperation(OperationId = "ListGamespaces")]
    [Authorize]
    public async Task<ActionResult<Gamespace[]>> ListGamespaces([FromQuery] GamespaceSearch model, CancellationToken ct)
    {
        if (!AuthorizeAll()) return Forbid();

        return Ok(
            await gamespaceService.List(model, Actor.Id, Actor.IsAdmin, Actor.IsObserver, Actor.Scope, ct)
        );
    }

    /// <summary>
    /// Preview a gamespace.
    /// </summary>
    /// <remarks></remarks>
    /// <param name="wid">Resource Id</param>
    /// <returns></returns>
    [HttpGet("api/preview/{wid}")]
    [SwaggerOperation(OperationId = "PreviewGamespace")]
    [Authorize]
    public async Task<ActionResult<GameState>> PreviewGamespace(string wid)
    {
        if (!AuthorizeAny(
            () => gamespaceService.HasValidUserScope(wid, Actor.Scope, Actor.Id).Result
        )) return Forbid();

        return Ok(
            await gamespaceService.Preview(wid)
        );
    }

    /// <summary>
    /// Load a gamespace state.
    /// </summary>
    /// <remarks></remarks>
    /// <param name="sid">Gamespace Id or Workspace Id</param>
    /// <returns></returns>
    [HttpGet("api/gamespace/{sid}")]
    [SwaggerOperation(OperationId = "LoadGamespace")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<GameState>> LoadGamespace(string sid)
    {
        if (!AuthorizeAny(
            () => sid == Actor.Id,  // from valid grader_apikey
            () => gamespaceService.CanInteract(sid, Actor.Id).Result
        )) return Forbid();

        return Ok(
            await gamespaceService.Load(sid, Actor.Id)
        );
    }

    [HttpGet("api/gamespace/{id}/challenge")]
    [SwaggerOperation(OperationId = "LoadGamespaceChallenge")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<ChallengeSpec>> LoadChallenge(string id)
    {
        if (!AuthorizeAny(
            () => Actor.IsObserver && gamespaceService.HasValidUserScopeGamespace(id, Actor.Scope).Result
        )) return Forbid();

        return Ok(
            await gamespaceService.LoadChallenge(id, Actor.IsAdmin)
        );
    }

    [HttpGet("api/gamespace/{gamespaceId}/challenge/progress")]
    [SwaggerOperation(OperationId = "LoadGamespaceChallengeProgress")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<ChallengeProgressView>> LoadChallengeProgress(string gamespaceId)
    {
        if (!AuthorizeAny(
            () => gamespaceId == Actor.Id,
            () => gamespaceService.CanInteract(gamespaceId, Actor.Id).Result
        )) return Forbid();

        var progress = await gamespaceService.LoadChallengeProgress(gamespaceId);
        return Ok(progress);
    }

    /// <summary>
    /// Register a gamespace on behalf of a user
    /// </summary>
    /// <param name="model"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    [HttpPost("api/gamespace")]
    [SwaggerOperation(OperationId = "RegisterGamespace")]
    [Authorize]
    public async Task<ActionResult<GameState>> RegisterGamespace([FromBody] GamespaceRegistration model, CancellationToken ct)
    {
        if (!AuthorizeAny(
            () => gamespaceService.HasValidUserScope(model.ResourceId, Actor.Scope, Actor.Id).Result
        )) return Forbid();

        if (string.IsNullOrEmpty(model.GraderUrl))
        {
            model.GraderUrl = string.Format(
                "{0}://{1}{2}",
                Request.Scheme,
                Request.Host,
                Url.Action("GradeChallenge")
            );
        }

        var result = await gamespaceService.Register(model, Actor);

        string token = Guid.NewGuid().ToString("n");

        if (model.Players.Length != 0)
        {
            string key = $"{TicketAuthentication.TicketCachePrefix}{token}";

            string value = model.Players
                .Select(p => $"{p.SubjectId}#{p.SubjectName}")
                .First();

            await distributedCache.SetStringAsync(key, value, _cacheOpts, ct);
        }

        result.LaunchpointUrl = $"{options.LaunchUrl}?t={token}&g={result.Id}";

        // if url is relative, make absolute
        if (!result.LaunchpointUrl.Contains("://"))
        {
            result.LaunchpointUrl = string.Format("{0}://{1}{2}{3}",
                Request.Scheme,
                Request.Host,
                Request.PathBase,
                result.LaunchpointUrl
            );
        }

        return Ok(result);
    }

    /// <summary>
    /// Update a gamespace.
    /// </summary>
    /// <param name="model">ChangedGamespace</param>
    /// <returns></returns>
    [HttpPut("api/gamespace")]
    [SwaggerOperation(OperationId = "UpdateGamespace")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<IActionResult> UpdateGamespace(ChangedGamespace model)
    {
        // TODO: replace CanManage with ActorCanEditGamespaces
        if (!AuthorizeAny(
            () => gamespaceService.CanManage(model.Id, Actor.Id).Result
        )) return Forbid();

        await gamespaceService.Update(model);

        return Ok();
    }

    /// <summary>
    /// Start a gamespace.
    /// </summary>
    /// <param name="id">Gamespace Id</param>
    /// <returns></returns>
    [HttpPost("api/gamespace/{id}/start")]
    [SwaggerOperation(OperationId = "StartGamespace")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<GameState>> StartGamespace(string id)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanManage(id, Actor.Id).Result
        )) return Forbid();

        var result = await gamespaceService.Start(id, Actor.IsBuilder);

        SendBroadcast(result, "UPDATE");

        return Ok(result);
    }

    /// <summary>
    /// Stop a gamespace.
    /// </summary>
    /// <param name="id">Gamespace Id</param>
    /// <returns></returns>
    [HttpPost("api/gamespace/{id}/stop")]
    [SwaggerOperation(OperationId = "StopGamespace")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<GameState>> StopGamespace(string id)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanManage(id, Actor.Id).Result
        )) return Forbid();

        var result = await gamespaceService.Stop(id);

        SendBroadcast(result, "UPDATE");

        return Ok(result);
    }

    /// <summary>
    /// Complete a gamespace.
    /// </summary>
    /// <param name="id">Gamespace Id</param>
    /// <returns></returns>
    [HttpPost("api/gamespace/{id}/complete")]
    [SwaggerOperation(OperationId = "CompleteGamespace")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<GameState>> CompleteGamespace(string id)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanManage(id, Actor.Id).Result
        )) return Forbid();

        var result = await gamespaceService.Complete(id);

        SendBroadcast(result, "UPDATE");

        return Ok(result);
    }

    /// <summary>
    /// Grade a challenge.
    /// </summary>
    /// <param name="model">SectionSubmission</param>
    /// <returns></returns>
    [HttpPost("api/gamespace/grade")]
    [SwaggerOperation(OperationId = "GradeChallenge")]
    [Authorize]
    public async Task<ActionResult<GameState>> GradeChallenge([FromBody] SectionSubmission model)
    {
        if (!AuthorizeAny(
            () => model.Id == Actor.Id,  // from valid grader_apikey
            () => gamespaceService.CanInteract(model.Id, Actor.Id).Result
        )) return Forbid();

        var result = await gamespaceService.Grade(model);

        SendBroadcast(result, "UPDATE");

        return Ok(result);
    }

    /// <summary>
    /// Regrade a challenge.
    /// </summary>
    /// <param name="id">SectionSubmission</param>
    /// <returns></returns>
    [HttpPost("api/gamespace/{id}/regrade")]
    [SwaggerOperation(OperationId = "RegradeChallenge")]
    [Authorize]
    public async Task<ActionResult<GameState>> RegradeChallenge(string id)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanManage(id, Actor.Id).Result
        )) return Forbid();

        return Ok(
            await gamespaceService.Regrade(id)
        );
    }

    /// <summary>
    /// Audit a challenge.
    /// </summary>
    /// <param name="id">SectionSubmission</param>
    /// <returns></returns>
    [HttpPost("api/gamespace/{id}/audit")]
    [SwaggerOperation(OperationId = "AuditChallenge")]
    [Authorize]
    public async Task<ActionResult<SectionSubmission[]>> AuditChallenge(string id)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanManage(id, Actor.Id).Result
        )) return Forbid();

        return Ok(
            (await gamespaceService.AuditSubmission(id)).ToArray()
        );
    }

    /// <summary>
    /// Delete a gamespace.
    /// </summary>
    /// <remarks>
    /// </remarks>
    /// <param name="id">Gamespace Id</param>
    /// <returns></returns>
    [HttpDelete("api/gamespace/{id}")]
    [SwaggerOperation(OperationId = "DeleteGamespace")]
    [Authorize]
    public async Task<ActionResult> DeleteGamespace(string id)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanInteract(id, Actor.Id).Result
        )) return Forbid();

        await gamespaceService.Delete(id, Actor.IsAdmin);

        SendBroadcast(new GameState { Id = id }, "OVER");

        return Ok();
    }

    [HttpPost("api/gamespace/{id}/invite")]
    [SwaggerOperation(OperationId = "GetGamespaceInvitation")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<JoinCode>> GetGamespaceInvitation(string id)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanManage(id, Actor.Id).Result
        )) return Forbid();

        return Ok(
            await gamespaceService.GenerateInvitation(id)
        );
    }

    /// <summary>
    /// Accept an invitation to a gamespace.
    /// </summary>
    /// <param name="code">Invitation Code</param>
    /// <returns></returns>
    [HttpPost("api/player/{code}")]
    [Authorize]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<bool>> BecomePlayer(string code)
    {
        await gamespaceService.Enlist(code, Actor);

        return Ok();
    }

    /// <summary>
    /// Accept an invitation to a gamespace.
    /// </summary>
    /// <param name="model">Enlistee</param>
    /// <param name="ct">CancellationToken</param>
    /// <returns></returns>
    [HttpPut("api/player/enlist")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult<Enlistment>> Enlist(Enlistee model, CancellationToken ct)
    {
        var result = await gamespaceService.Enlist(model);

        // Set an auth ticket
        string key = $"{TicketAuthentication.TicketCachePrefix}{result.Token}";

        string value = $"{result.Token}#{model.SubjectName ?? "anonymous"}";

        await distributedCache.SetStringAsync(key, value, _cacheOpts, ct);

        return Ok(result);
    }

    /// <summary>
    /// Remove a player from a gamespace.
    /// </summary>
    /// <param name="id">Gamespace Id</param>
    /// <param name="subjectId">Subject Id of target member</param>
    /// <returns></returns>
    [HttpDelete("api/gamespace/{id}/player/{subjectId}")]
    [SwaggerOperation(OperationId = "RemovePlayer")]
    [Authorize]
    public async Task<ActionResult<bool>> RemovePlayer([FromRoute] string id, string subjectId)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanManage(id, Actor.Id).Result
        )) return Forbid();

        await gamespaceService.Delist(id, subjectId);

        return Ok();
    }

    /// <summary>
    /// List gamespace players.
    /// </summary>
    /// <param name="id">Gamespace Id</param>
    /// <returns></returns>
    [HttpGet("api/players/{id}")]
    [SwaggerOperation(OperationId = "GetGamespacePlayers")]
    [Authorize]
    public async Task<ActionResult<Player[]>> GetGamespacePlayers(string id)
    {
        if (!AuthorizeAny(
            () => gamespaceService.CanInteract(id, Actor.Id).Result
        )) return Forbid();

        return Ok(
            await gamespaceService.Players(id)
        );
    }

    private void SendBroadcast(GameState gameState, string action)
    {
        Hub.Clients.Group(gameState.Id).GameEvent(
            new BroadcastEvent<GameState>(
                User,
                "GAME." + action.ToUpper(),
                gameState
            )
        );
    }

}
