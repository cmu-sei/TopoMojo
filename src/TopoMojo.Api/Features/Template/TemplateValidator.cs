// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Mvc.Filters;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Controllers;

namespace TopoMojo.Api.Validators;

public class TemplateValidator(
    ITemplateStore store,
    ILogger<TemplateValidator> logger
    ) : BaseValidationFilter
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

                case TemplateSearch search:
                    await Validate(key, search);
                    break;

                case ChangedTemplate model:
                    await Validate(key, model);
                    break;

                case TemplateLink model:
                    await Validate(key, model);
                    break;

                case TemplateReLink model:
                    await Validate(key, model);
                    break;

                case NewTemplateDetail model:
                    await Validate(key, model);
                    break;

                case ChangedTemplateDetail model:
                    await Validate(key, model);
                    break;

                case TemplateClone model:
                    await Validate(key, model);
                    break;

                case TemplateDetail model:
                    await Validate(key, model);
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

    private async Task Validate(string key, TemplateDetail model)
    {
        await Exists(key, model.Id);
    }

    private async Task Validate(string key, ChangedTemplateDetail model)
    {
        await Exists(key, model.Id);
    }

    private async Task Validate(string key, NewTemplateDetail model)
    {
        var entity = await store.Retrieve(model.Id);
        if (entity is not null)
            Problems.Add(new Problem(key, Message.ResourceAlreadyExists));
    }

    private async Task Validate(string key, TemplateClone model)
    {
        await Exists(key, model.Id);
    }

    private async Task Validate(string key, TemplateReLink model)
    {
        await Exists(key, model.TemplateId);
        await Exists(key, model.ParentId);
        if (Problems.Count == 0)
        {
            var t = await store.Retrieve(model.TemplateId);
            if (t?.WorkspaceId != model.WorkspaceId)
                Problems.Add(new Problem(key, Message.ResourceNotFound));
        }
    }

    private async Task Validate(string key, TemplateLink model)
    {
        await Exists(key, model.TemplateId);
        if ((await store.DbContext.Workspaces.FindAsync(model.WorkspaceId)) == null)
            Problems.Add(new Problem(key, Message.ResourceNotFound));
    }

    private async Task Validate(string key, ChangedTemplate model)
    {
        await Exists(key, model.Id);
        if (model.Name.IsEmpty())
            Problems.Add(new Problem(key, "Missing value for required property 'name'"));
    }

}
