// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using TopoMojo.Api;
using TopoMojo.Api.Services;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class FileUploadStartupExtensions
    {
        public static IServiceCollection AddFileUpload(
            this IServiceCollection services,
            FileUploadOptions options
        )
        {
            return services
                .AddScoped<IFileUploadHandler, FileUploadHandler>()
                .AddSingleton(_ => options)
                .AddSingleton<IFileUploadMonitor, FileUploadMonitor>();
        }
    }
}
