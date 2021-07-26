// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using TopoMojo.Api;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class AuthenticationStartupExtensions
    {
        public static IServiceCollection AddConfiguredAuthentication(
            this IServiceCollection services,
            OidcOptions oidc
        ) {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services
                .AddScoped<IClaimsTransformation, UserClaimsTransformation>()

                .AddAuthentication(options =>
                {
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })

                .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Audience = oidc.Audience;
                    options.Authority = oidc.Authority;
                    options.RequireHttpsMetadata = oidc.RequireHttpsMetadata;
                })

                .AddApiKey(ApiKeyAuthentication.AuthenticationScheme, options => {})

                .AddTicketAuthentication(TicketAuthentication.AuthenticationScheme, options => {})

                .AddCookie(AppConstants.CookieScheme, opt =>
                {
                    opt.ExpireTimeSpan = new TimeSpan(0, oidc.MksCookieMinutes, 0);
                    opt.Cookie = new CookieBuilder
                    {
                        Name = AppConstants.CookieScheme,
                    };
                    opt.Events.OnRedirectToAccessDenied = ctx => {
                        ctx.HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                        return System.Threading.Tasks.Task.CompletedTask;
                    };
                    opt.Events.OnRedirectToLogin = ctx => {
                        ctx.HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return System.Threading.Tasks.Task.CompletedTask;
                    };
                })
            ;

            return services;
        }
    }
}
