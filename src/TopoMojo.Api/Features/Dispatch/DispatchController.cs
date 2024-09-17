// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Api.Validators;

namespace TopoMojo.Api.Controllers;

[Authorize]
[ApiController]
[TypeFilter<DispatchValidator>]
public class DispatchController(
    ILogger<AdminController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    DispatchService dispatchService,
    GamespaceService gamespaceService
    ) : _Controller(logger, hub)
{

    /// <summary>
    /// Create new dispatch
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    [HttpPost("api/dispatch")]
    [SwaggerOperation(OperationId = "CreateDispatch")]
    public async Task<ActionResult<Dispatch>> Create([FromBody] NewDispatch model)
    {
        if (!AuthorizeAny(
            () => Actor.IsAdmin,
            () => model.TargetGroup == Actor.Id, // gamespace agent
            () => gamespaceService.CanManage(model.TargetGroup, Actor.Id).Result,
            () => Actor.IsObserver && gamespaceService.HasValidUserScopeGamespace(model.TargetGroup, Actor.Scope).Result
        )) return Forbid();

        var result = await dispatchService.Create(model);

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
        var model = await dispatchService.Retrieve(id);

        if (!AuthorizeAny(
            () => Actor.IsAdmin,
            () => model.TargetGroup == Actor.Id, // gamespace agent
            () => gamespaceService.CanManage(model.TargetGroup, Actor.Id).Result,
            () => Actor.IsObserver && gamespaceService.HasValidUserScopeGamespace(model.TargetGroup, Actor.Scope).Result
        )) return Forbid();

        return await dispatchService.Retrieve(id);
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
        var dispatch = await dispatchService.Retrieve(model.Id);

        if (!AuthorizeAny(
            () => Actor.IsAdmin,
            () => dispatch.TargetGroup == Actor.Id, // gamespace agent
            () => gamespaceService.CanManage(dispatch.TargetGroup, Actor.Id).Result,
            () => Actor.IsObserver && gamespaceService.HasValidUserScopeGamespace(dispatch.TargetGroup, Actor.Scope).Result
        )) return Forbid();

        dispatch = await dispatchService.Update(model);

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
        var entity = await dispatchService.Retrieve(id);

        if (!AuthorizeAny(
            () => Actor.IsAdmin,
            () => entity.TargetGroup == Actor.Id, // gamespace agent
            () => gamespaceService.CanManage(entity.TargetGroup, Actor.Id).Result,
            () => Actor.IsObserver && gamespaceService.HasValidUserScopeGamespace(entity.TargetGroup, Actor.Scope).Result
        )) return Forbid();

        await dispatchService.Delete(id);

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
        if (!AuthorizeAny(
            () => Actor.IsAdmin,
            () => model.gs == Actor.Id, // gamespace agent
            () => gamespaceService.CanManage(model.gs, Actor.Id).Result,
            () => Actor.IsObserver && gamespaceService.HasValidUserScopeGamespace(model.gs, Actor.Scope).Result
        )) return Forbid();

        return await dispatchService.List(model, ct);
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
