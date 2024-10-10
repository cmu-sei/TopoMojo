// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Mvc.Filters;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Controllers;

namespace TopoMojo.Api.Validators;

public class DispatchValidator(
    IDispatchStore store,
    ILogger<DispatchValidator> logger
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
                }
                break;

                case NewDispatch model:
                await Validate(key, model);
                break;

                case ChangedDispatch model:
                await Validate(key, model);
                break;

                case DispatchSearch search:
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

    private async Task Exists(string key, string? id)
    {
        var entity = await store.Retrieve(id ?? "invalid");
        if (entity is null)
            Problems.Add(new Problem(key, Message.ResourceNotFound));
    }

    private async Task Validate(string key, NewDispatch model)
    {
        if (string.IsNullOrEmpty(model.Trigger))
            Problems.Add(new Problem(key, Message.InvalidPropertyValue + " 'trigger'"));

        if (await store.DbContext.Gamespaces.FindAsync(model.TargetGroup) == null)
            Problems.Add(new Problem(key, Message.ResourceNotFound));
    }

    private async Task Validate(string key, ChangedDispatch model)
    {
        await Exists(key, model.Id);
    }

    private async Task Validate(string key, DispatchSearch model)
    {
        await Validate(key, model as Search);
        if (await store.DbContext.Gamespaces.FindAsync(model.gs) == null)
            Problems.Add(new Problem(key, Message.ResourceNotFound));
    }

}
