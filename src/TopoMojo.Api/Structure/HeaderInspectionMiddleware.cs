// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System.Text;
using TopoMojo.Api;

namespace TopoMojo.Api
{
    public class HeaderInspectionMiddleware(
        RequestDelegate next,
        ILogger<HeaderInspectionMiddleware> logger
        )
    {
        public async Task Invoke(HttpContext context)
        {
            var sb = new StringBuilder($"Request Headers: {context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase} from {context.Connection.RemoteIpAddress}\n");

            sb.AppendLine($"\t{context.Request.Method} {context.Request.Path.Value} {context.Request.Protocol}");

            foreach (var header in context.Request.Headers)
            {
                string val = header.Value;

                if (header.Key.StartsWith("Authorization"))
                    val = header.Value.ToString().Split(' ').First() + " **redacted**";

                sb.AppendLine($"\t{header.Key}: {val}");
            }

            logger.LogInformation("{headers}", sb.ToString());

            await next(context);
        }
    }
}

namespace Microsoft.AspNetCore.Builder
{
    public static class StartUpExtensions
    {
        public static IApplicationBuilder UseHeaderInspection(
            this IApplicationBuilder builder
        )
        {
            return builder.UseMiddleware<HeaderInspectionMiddleware>();
        }
    }
}
