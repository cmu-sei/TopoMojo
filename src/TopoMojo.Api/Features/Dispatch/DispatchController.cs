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

namespace TopoMojo.Api.Controllers
{
    [Authorize]
    [ApiController]
    public class DispatchController : _Controller
    {
        DispatchService DispatchService { get; }
        GamespaceService GamespaceService { get; }

        public DispatchController(
            ILogger<AdminController> logger,
            IHubContext<AppHub, IHubEvent> hub,
            DispatchValidator validator,
            DispatchService dispatchService,
            GamespaceService gamespaceService
        ) : base(logger, hub, validator)
        {
            DispatchService = dispatchService;
            GamespaceService = gamespaceService;
        }

        /// <summary>
        /// Create new dispatch
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPost("api/dispatch")]
        [SwaggerOperation(OperationId = "CreateDispatch")]
        public async Task<ActionResult<Dispatch>> Create([FromBody] NewDispatch model)
        {
            await Validate(model);

            if (!AuthorizeAny(
                () => Actor.IsAdmin,
                () => model.TargetGroup == Actor.Id, // gamespace agent
                () => GamespaceService.CanManage(model.TargetGroup, Actor.Id).Result,
                () => Actor.IsObserver && GamespaceService.HasValidUserScopeGamespace(model.TargetGroup, Actor.Scope).Result
            )) return Forbid();

            var result = await DispatchService.Create(model);

            SendBroadcast(result, "CREATE");

            return result;
        }

        /// <summary>
        /// Retrieve dispatch
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("api/dispatch/{id}")]
        [SwaggerOperation(OperationId = "GetDispatch")]
        public async Task<ActionResult<Dispatch>> Retrieve([FromRoute] string id)
        {
            await Validate(new Entity { Id = id });

            var model = await DispatchService.Retrieve(id);

            if (!AuthorizeAny(
                () => Actor.IsAdmin,
                () => model.TargetGroup == Actor.Id, // gamespace agent
                () => GamespaceService.CanManage(model.TargetGroup, Actor.Id).Result,
                () => Actor.IsObserver && GamespaceService.HasValidUserScopeGamespace(model.TargetGroup, Actor.Scope).Result
            )) return Forbid();

            return await DispatchService.Retrieve(id);
        }

        /// <summary>
        /// Change dispatch
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut("api/dispatch")]
        [SwaggerOperation(OperationId = "UpdateDispatch")]
        public async Task<ActionResult> Update([FromBody] ChangedDispatch model)
        {
            await Validate(model);

            var dispatch = await DispatchService.Retrieve(model.Id);

            if (!AuthorizeAny(
                () => Actor.IsAdmin,
                () => dispatch.TargetGroup == Actor.Id, // gamespace agent
                () => GamespaceService.CanManage(dispatch.TargetGroup, Actor.Id).Result,
                () => Actor.IsObserver && GamespaceService.HasValidUserScopeGamespace(dispatch.TargetGroup, Actor.Scope).Result
            )) return Forbid();

            dispatch = await DispatchService.Update(model);

            SendBroadcast(
                dispatch,
                "UPDATE"
            );
            return Ok();
        }

        /// <summary>
        /// Delete dispatch
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("/api/dispatch/{id}")]
        [SwaggerOperation(OperationId = "DeleteDispatch")]
        public async Task<ActionResult> Delete([FromRoute] string id)
        {
            await Validate(new Entity { Id = id });

            var entity = await DispatchService.Retrieve(id);

            if (!AuthorizeAny(
                () => Actor.IsAdmin,
                () => entity.TargetGroup == Actor.Id, // gamespace agent
                () => GamespaceService.CanManage(entity.TargetGroup, Actor.Id).Result,
                () => Actor.IsObserver && GamespaceService.HasValidUserScopeGamespace(entity.TargetGroup, Actor.Scope).Result
            )) return Forbid();

            await DispatchService.Delete(id);

            SendBroadcast(new Dispatch { Id = id, TargetGroup = entity.TargetGroup }, "DELETE");
            return Ok();
        }

        /// <summary>
        /// Find dispatches
        /// </summary>
        /// <param name="model"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        [HttpGet("/api/dispatches")]
        [SwaggerOperation(OperationId = "ListDispatches")]
        public async Task<ActionResult<Dispatch[]>> List([FromQuery] DispatchSearch model, CancellationToken ct)
        {
            await Validate(model);

            if (!AuthorizeAny(
                () => Actor.IsAdmin,
                () => model.gs == Actor.Id, // gamespace agent
                () => GamespaceService.CanManage(model.gs, Actor.Id).Result,
                () => Actor.IsObserver && GamespaceService.HasValidUserScopeGamespace(model.gs, Actor.Scope).Result
            )) return Forbid();

            return await DispatchService.List(model, ct);
        }

        private void SendBroadcast(Dispatch dispatch, string action)
        {
            Hub.Clients.Group(dispatch.TargetGroup).DispatchEvent(
                new BroadcastEvent<Dispatch>(
                    User,
                    "DISPATCH." + action.ToUpper(),
                    dispatch
                )
            );
        }
    }
}
