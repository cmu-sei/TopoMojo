// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TopoMojo.Api;

namespace TopoMojo.Api
{
    public class HeaderInspectionMiddleware
    {
        public HeaderInspectionMiddleware(
            RequestDelegate next,
            ILogger<HeaderInspectionMiddleware> logger
        ){
            _next = next;
            _logger = logger;
        }
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

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

            _logger.LogInformation(sb.ToString());

            await _next(context);
        }
    }
}

namespace Microsoft.AspNetCore.Builder
{
    public static class StartUpExtensions
    {
        public static IApplicationBuilder UseHeaderInspection (
            this IApplicationBuilder builder
        )
        {
            return builder.UseMiddleware<HeaderInspectionMiddleware>();
        }
    }
}
