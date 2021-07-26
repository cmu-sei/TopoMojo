// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Api.Validators;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Controllers
{
    [Authorize]
    [ApiController]
    public class GamespaceController : _Controller
    {
        public GamespaceController(
            ILogger<AdminController> logger,
            IHubContext<AppHub, IHubEvent> hub,
            GamespaceValidator validator,
            CoreOptions options,
            GamespaceService gamespaceService,
            IHypervisorService podService,
            IDistributedCache distributedCache
        ) : base(logger, hub, validator)
        {
            _svc = gamespaceService;
            _pod = podService;
            _distCache = distributedCache;
            _options = options;

            _cacheOpts = new DistributedCacheEntryOptions {
                SlidingExpiration = new TimeSpan(0, 0, 60)
            };
        }

        private readonly IHypervisorService _pod;
        private readonly GamespaceService _svc;
        private readonly IDistributedCache _distCache;
        private readonly CoreOptions _options;
        private DistributedCacheEntryOptions _cacheOpts;

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
        public async Task<ActionResult<Gamespace[]>> ListGamespaces([FromQuery]GamespaceSearch model, CancellationToken ct)
        {
            await Validate(model);

            AuthorizeAll();

            return Ok(
                await _svc.List(model, Actor.Id, Actor.IsAdmin, ct)
            );
        }

        /// <summary>
        /// Preview a gamespace.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="id">Resource Id</param>
        /// <returns></returns>
        [HttpGet("api/preview/{id}")]
        [SwaggerOperation(OperationId = "PreviewGamespace")]
        [Authorize]
        public async Task<ActionResult<GameState>> PreviewGamespace(string id)
        {
            await Validate(new WorkspaceEntity { Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.HasValidUserScope(id, Actor.Scope, Actor.Id).Result
            );

            return Ok(
                await _svc.Preview(id)
            );
        }

        /// <summary>
        /// Load a gamespace state.
        /// </summary>
        /// <remarks></remarks>
        /// <param name="id">Gamespace Id or Workspace Id</param>
        /// <returns></returns>
        [HttpGet("api/gamespace/{id}")]
        [SwaggerOperation(OperationId = "LoadGamespace")]
        [Authorize(AppConstants.AnyUserPolicy)]
        public async Task<ActionResult<GameState>> LoadGamespace(string id)
        {
            await Validate(new SpaceEntity { Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanInteract(id, Actor.Id).Result
            );

            return Ok(
                await _svc.Load(id, Actor.Id)
            );
        }

        [HttpGet("api/gamespace/{id}/challenge")]
        [SwaggerOperation(OperationId = "LoadGamespaceChallenge")]
        [Authorize(AppConstants.AdminOnlyPolicy)]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<ActionResult<ChallengeSpec>> LoadChallenge(string id)
        {
            await Validate(new Entity { Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin
            );

            return Ok(
                await _svc.LoadChallenge(id, Actor.IsAdmin)
            );
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
        public async Task<ActionResult<GameState>> RegisterGamespace([FromBody]GamespaceRegistration model, CancellationToken ct)
        {
            await Validate(model);

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.HasValidUserScope(model.ResourceId, Actor.Scope, Actor.Id).Result
            );

            var result = await _svc.Register(model, Actor);

            string token = Guid.NewGuid().ToString("n");

            if (model.Players.Any())
            {
                string key = $"{TicketAuthentication.TicketCachePrefix}{token}";

                string value = model.Players
                    .Select(p => $"{p.SubjectId}#{p.SubjectName}")
                    .First();

                await _distCache.SetStringAsync(key, value, _cacheOpts, ct);
            }

            result.LaunchpointUrl = $"{_options.LaunchUrl}?t={token}&g={result.Id}";

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
        /// Start a gamespace.
        /// </summary>
        /// <param name="id">Gamespace Id</param>
        /// <returns></returns>
        [HttpPost("api/gamespace/{id}/start")]
        [SwaggerOperation(OperationId = "StartGamespace")]
        [Authorize(AppConstants.AnyUserPolicy)]
        public async Task<ActionResult<GameState>> StartGamespace(string id)
        {
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanInteract(id, Actor.Id).Result
            );

            return Ok(
                await _svc.Start(id)
            );
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
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanInteract(id, Actor.Id).Result
            );

            return Ok(
                await _svc.Stop(id)
            );
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
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanInteract(id, Actor.Id).Result
            );

            return Ok(
                await _svc.Complete(id)
            );
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
            await Validate(new Entity{ Id = model.Id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanInteract(model.Id, Actor.Id).Result
            );

            return Ok(
                await _svc.Grade(model)
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
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanInteract(id, Actor.Id).Result
            );

            await _svc.Delete(id, Actor.IsAdmin);

            SendBroadcast(new GameState{Id = id}, "OVER");

            return Ok();
        }

        [HttpPost("api/gamespace/{id}/invite")]
        [SwaggerOperation(OperationId = "GetGamespaceInvitation")]
        [Authorize]
        public async Task<ActionResult<JoinCode>> GetGamespaceInvitation (string id)
        {
            await Validate(new Entity { Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanManage(id, Actor.Id).Result
            );

            return Ok(
                await _svc.GenerateInvitation(id)
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
            await _svc.Enlist(code, Actor);

            return Ok();
        }

        /// <summary>
        /// Remove a player from a gamespace.
        /// </summary>
        /// <param name="id">Gamespace Id</param>
        /// <param name="sid">Subject Id of target member</param>
        /// <returns></returns>
        [HttpDelete("api/gamespace/{id}/player/{sid}")]
        [SwaggerOperation(OperationId = "RemovePlayer")]
        [Authorize]
        public async Task<ActionResult<bool>> RemovePlayer([FromRoute] string id, string sid)
        {
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanManage(id, Actor.Id).Result
            );

            await _svc.Delist(id, sid);

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
            await Validate(new Entity{ Id = id });

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => _svc.CanInteract(id, Actor.Id).Result
            );

            return Ok(
                await _svc.Players(id)
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
}
