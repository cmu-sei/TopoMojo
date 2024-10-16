// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Mvc.Filters;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Controllers;

namespace TopoMojo.Api.Validators
{
    public class WorkspaceValidator(
        IWorkspaceStore store,
        ILogger<WorkspaceValidator> logger
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

                    case NewWorkspace model:
                        await Validate(key, model);
                        break;

                    case ChangedWorkspace model:
                        await Validate(key, model);
                        break;

                    case RestrictedChangedWorkspace model:
                        await Validate(key, model);
                        break;

                    case ChallengeSpec model:
                        await Validate(key, model);
                        break;

                    case WorkspaceSearch search:
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

        private async Task Validate(string key, NewWorkspace model)
        {
            if (model.Name.IsEmpty())
                Problems.Add(new Problem(key, "Missing value for required property"));
            await Task.CompletedTask;
        }

        private async Task Validate(string key, ChangedWorkspace model)
        {
            await Exists(key, model.Id);
            if (model.Name.IsEmpty())
                Problems.Add(new Problem(key, "Missing value for required property 'name'"));
        }

        private async Task Validate(string key, RestrictedChangedWorkspace model)
        {
            await Exists(key, model.Id);
            if (model.Name.IsEmpty())
                Problems.Add(new Problem(key, "Missing value for required property 'name'"));
        }

        private async Task Validate(string key, ChallengeSpec model)
        {
            await Task.CompletedTask;
        }
    }
}
