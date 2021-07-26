// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using TopoMojo;
using TopoMojo.Api;

namespace  Microsoft.Extensions.DependencyInjection
{
    public static class AuthorizationStartupExtensions
    {
        public static IServiceCollection AddConfiguredAuthorization(
            this IServiceCollection services
        ) {
            services.AddAuthorization(_ =>
            {
                _.DefaultPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        ApiKeyAuthentication.AuthenticationScheme
                    ).Build()
                ;

                _.AddPolicy(AppConstants.AnyUserPolicy, new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        ApiKeyAuthentication.AuthenticationScheme,
                        AppConstants.CookieScheme,
                        TicketAuthentication.AuthenticationScheme
                    )
                    .Build()
                );

                _.AddPolicy(AppConstants.AdminOnlyPolicy, new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme,
                        ApiKeyAuthentication.AuthenticationScheme
                    )
                    .RequireClaim(AppConstants.RoleClaimName, TopoMojo.Api.Models.UserRole.Administrator.ToString())
                    .Build()
                );

                _.AddPolicy(AppConstants.TicketOnlyPolicy, new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(TicketAuthentication.AuthenticationScheme)
                    .Build()
                );

                _.AddPolicy(AppConstants.CookiePolicy, new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddAuthenticationSchemes(AppConstants.CookieScheme)
                    .Build()
                );

            });

            return services;
        }
    }

}
