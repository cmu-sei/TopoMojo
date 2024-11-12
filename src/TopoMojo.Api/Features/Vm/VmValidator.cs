// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Mvc.Filters;
using TopoMojo.Api.Controllers;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Validators;

public class VmValidator(
    IHypervisorService pod,
    IWorkspaceStore store,
    ILogger<VmValidator> logger
) : BaseValidationFilter
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var key in context.ActionArguments.Keys)
        {
            var value = context.ActionArguments[key];

            switch (value)
            {

                case VmOperation model:
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

    private async Task Validate(string key, VmOperation model)
    {
        if (model.Type == VmOperationType.Save)
        {
            string isolationId = model.Id.Contains('#')
                ? model.Id.Tag()
                : (await pod.Load(model.Id))?.Name.Tag();

            if (await store.HasActiveGames(isolationId))
            {
                Problems.Add(new Problem(key, Message.WorkspaceNotIsolated));
            }
        }
    }
}
