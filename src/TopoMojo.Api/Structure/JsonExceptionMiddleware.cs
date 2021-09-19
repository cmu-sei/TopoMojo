// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TopoMojo.Api;
using TopoMojo.Api.Exceptions;

namespace TopoMojo.Api
{
    public class JsonExceptionMiddleware
    {
        public JsonExceptionMiddleware(
            RequestDelegate next,
            ILogger<JsonExceptionMiddleware> logger
        )
        {
            _next = next;
            _logger = logger;
        }
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public async Task Invoke(HttpContext context, IMemoryCache cache)
        {
            try {
                await _next(context);
            }
            catch (Exception ex)
            {
                if (!(ex is ResourceNotFound))
                {
                    _logger.LogError(ex, "Error");

                    var errorList = cache.Get<List<TimestampedException>>(AppConstants.ErrorListCacheKey) ?? new List<TimestampedException>();
                    errorList.Add(new TimestampedException
                    {
                        Timestamp = DateTimeOffset.UtcNow,
                        Message = ex.Message,
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
                    ) {
                        context.Response.StatusCode = 400;

                        message = type.Name
                            .Split('.')
                            .Last()
                            .Replace("Exception", "");

                        if (ex is ArgumentException)
                            message += $" {ex.Message}";
                    }

                    await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = message }));
                }
            }

        }
    }
}

namespace Microsoft.AspNetCore.Builder
{
    public static class JsonExceptionStartupExtensions
    {
        public static IApplicationBuilder UseJsonExceptions (
            this IApplicationBuilder builder
        )
        {
            return builder.UseMiddleware<JsonExceptionMiddleware>();
        }
    }
}
