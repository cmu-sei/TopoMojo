// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Mvc.Filters;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Controllers;

public class UserValidator(
    IUserStore store,
    ILogger<UserValidator> logger
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

                case UserSearch search:
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
}
