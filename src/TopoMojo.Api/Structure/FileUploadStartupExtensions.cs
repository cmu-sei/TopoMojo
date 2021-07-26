// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

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
                .AddSingleton<FileUploadOptions>(_ => options)
                .AddSingleton<IFileUploadMonitor, FileUploadMonitor>();
        }
    }
}
