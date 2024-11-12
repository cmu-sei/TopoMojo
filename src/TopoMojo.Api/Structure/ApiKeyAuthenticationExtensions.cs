// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using Microsoft.AspNetCore.Authentication;
using TopoMojo.Api;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ApiKeyAuthenticationExtensions
    {
        public static AuthenticationBuilder AddApiKey(
            this AuthenticationBuilder builder,
            string scheme,
            Action<ApiKeyAuthenticationOptions> options
        )
        {

            builder.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                scheme ?? ApiKeyAuthentication.AuthenticationScheme,
                options
            );

            return builder;
        }
    }
}
