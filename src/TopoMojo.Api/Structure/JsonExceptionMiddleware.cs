// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TopoMojo.Api;
using TopoMojo.Api.Exceptions;
using TopoMojo.Hypervisor.Exceptions;

namespace TopoMojo.Api
{
    public class JsonExceptionMiddleware(
        RequestDelegate next,
        ILogger<JsonExceptionMiddleware> logger
        )
    {

        public async Task Invoke(HttpContext context, IMemoryCache cache)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                if (ex is not ResourceNotFound)
                {
                    // admin-only detail goes to the log, never to the response
                    string detail = (ex as HypervisorException)?.Detail;

                    if (detail is null)
                        logger.LogError(ex, "Error");
                    else
                        logger.LogError(ex, "Error: {detail}", detail);

                    var errorList = cache.Get<List<TimestampedException>>(AppConstants.ErrorListCacheKey) ?? [];
                    errorList.Add(new TimestampedException
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        Message = detail is null ? ex.Message : $"{ex.Message} {detail}",
                        StackTrace = ex.StackTrace
                    });

                    cache.Set(
                        AppConstants.ErrorListCacheKey,
                        errorList.OrderByDescending(e => e.Timestamp).Take(50).ToList()
                    );
                }

                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 500;
                    string message = "Error";
                    Type type = ex.GetType();

                    if (
                        ex is InvalidOperationException ||
                        ex is ArgumentException ||
                        type.Namespace.StartsWith("TopoMojo")
                    )
                    {
                        context.Response.StatusCode = 400;

                        message = type.Name
                            .Split('.')
                            .Last()
                            .Replace("Exception", "");

                        if (ex is ArgumentException)
                            message += $" {ex.Message}";
                    }

                    if (ex is HypervisorException)
                    {
                        message = ex.Message;
                    }

                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
                }
            }

        }
    }
}

namespace Microsoft.AspNetCore.Builder
{
    public static class JsonExceptionStartupExtensions
    {
        public static IApplicationBuilder UseJsonExceptions(
            this IApplicationBuilder builder
        )
        {
            return builder.UseMiddleware<JsonExceptionMiddleware>();
        }
    }
}
