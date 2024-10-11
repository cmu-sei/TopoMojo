// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Models;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TopoMojo.Api.Controllers;

public class GamespaceValidator(
    IGamespaceStore store,
    ILogger<GamespaceValidator> logger
    ) : _ValidationFilter
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var key in context.ActionArguments.Keys)
        {
            var value = context.ActionArguments[key];

            switch (value)
            {
                case string val:
                    switch (key.ToLower())
                    {
                        case "id":
                            await Exists(key, val);
                            break;

                        case "wid":
                            await WorkspaceExists(key, val);
                            break;

                        case "sid":
                            await SpaceExists(key, val);
                            break;
                    }
                    break;

                case ChangedGamespace model:
                    await Validate(key, model);
                    break;

                case SectionSubmission model:
                    await Validate(key, model);
                    break;

                case Player model:
                    await Validate(key, model);
                    break;

                case GamespaceRegistration model:
                    await Validate(key, model);
                    break;

                case GamespaceSearch search:
                    await Validate(key, search);
                    break;

                default:
                    logger.LogWarning("No validation found for {key} {value}", key, value.GetType().Name);
                    break;

            }
        }

        // call this after all the validation checks
        await base.OnActionExecutionAsync(context, next);
    }

    private async Task Exists(string key, string id)
    {
        var entity = await store.Retrieve(id ?? "invalid");
        if (entity is null)
            Problems.Add(new Problem(key, Message.ResourceNotFound));
    }

    private async Task WorkspaceExists(string key, string id)
    {
        var entity = await store.DbContext.Workspaces.FindAsync(id ?? "invalid");
        if (entity is null)
            Problems.Add(new Problem(key, Message.ResourceNotFound));
    }

    private async Task SpaceExists(string key, string id)
    {
        var gs = await store.Retrieve(id ?? "invalid");
        var ws = await store.DbContext.Workspaces.FindAsync(id ?? "invalid");
        if (gs is null && ws is null)
            Problems.Add(new Problem(key, Message.ResourceNotFound));
    }


    private async Task Validate(string key, GamespaceRegistration model)
    {
        await SpaceExists(key, model.ResourceId);
    }

    private async Task Validate(string key, ChangedGamespace model)
    {
        await Exists(key, model.Id);
    }

    private async Task Validate(string key, SectionSubmission model)
    {
        await Exists(key, model.Id);
    }

    private async Task Validate(string key, Player model)
    {
        var entity = await store.DbContext.Players.FindAsync(model.SubjectId, model.GamespaceId);
        if (entity is null)
            Problems.Add(new Problem(key, Message.ResourceNotFound));
    }
}
