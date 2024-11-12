// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Controllers;

public class BaseValidationFilter : IAsyncActionFilter
{
    public BaseValidationFilter()
    {
        Problems = [];
    }

    protected List<Problem> Problems { get; }

    public virtual async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (Problems.Count != 0)
        {
            foreach (var problem in Problems)
                context.ModelState.AddModelError(problem.Key, problem.Value);

            context.Result = ((ControllerBase)context.Controller).ValidationProblem();

            return;
        }

        await next();
    }

    protected Task Validate(string key, Search model, int maxQueryTake = 0)
    {
        // if take limited and none requested, take limit
        if (maxQueryTake > 0 && model.Take == 0)
            model.Take = maxQueryTake;

        if (
            maxQueryTake > 0 &&
            model.Take > 0 &&
            model.Take > maxQueryTake
        )
        {
            Problems.Add(new Problem("Take", Message.MaximumTakeExceeded));
        }

        return Task.CompletedTask;
    }

    protected record class Problem(string Key, string Value);
}
