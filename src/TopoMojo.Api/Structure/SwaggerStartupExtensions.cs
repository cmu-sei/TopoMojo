// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Models;
using TopoMojo.Api;

namespace Microsoft.Extensions.DependencyInjection
{

    public static class SwaggerStartupExtensions
    {
        public static IServiceCollection AddSwagger(
            this IServiceCollection services,
            OidcOptions oidc,
            OpenApiOptions openapi
        )
        {
            string xmlDoc = Assembly.GetExecutingAssembly().GetName().Name + ".xml";

            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = openapi.ApiName,
                    Version = "v1",
                    Description = "API documentation and interaction"
                });

                options.EnableAnnotations();

#if DEBUG
                string[] files = Directory.GetFiles("bin", xmlDoc, SearchOption.AllDirectories);

                if (files.Length > 0)
                    options.IncludeXmlComments(files[0]);
#else
                if (File.Exists(xmlDoc))
                    options.IncludeXmlComments(xmlDoc);
#endif

                if (!string.IsNullOrEmpty(oidc.Authority))
                {
                    // this displays *all* flows allowed, which is a bit confusing at the ui
                    // so not adding it at this point
                    // options.AddSecurityDefinition("oidc", new OpenApiSecurityScheme
                    // {
                    //     Type = SecuritySchemeType.OpenIdConnect,
                    //     OpenIdConnectUrl = new Uri($"{oidc.Authority}/.well-known/openid-configuration"),
                    // });

                    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                    {

                        Type = SecuritySchemeType.OAuth2,

                        Flows = new OpenApiOAuthFlows
                        {
                            AuthorizationCode = new OpenApiOAuthFlow
                            {
                                AuthorizationUrl = new Uri(
                                    openapi.Client.AuthorizationUrl
                                    ?? $"{oidc.Authority}/connect/authorize"
                                ),
                                TokenUrl = new Uri(
                                    openapi.Client.TokenUrl
                                    ?? $"{oidc.Authority}/connect/token"
                                ),
                                Scopes = new Dictionary<string, string>
                                {
                                    { oidc.Audience, "User Access" }
                                }
                            }
                        },
                    });

                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                            },
                            new[] { oidc.Audience }
                        }
                    });
                }
            });

            return services;
        }

        public static IApplicationBuilder UseConfiguredSwagger(
            this IApplicationBuilder app,
            OpenApiOptions openapi,
            string audience,
            string pathbase
        )
        {
            app.UseSwagger(cfg =>
            {
                cfg.RouteTemplate = "api/{documentName}/openapi.json";
            });

            app.UseSwaggerUI(cfg =>
            {
                cfg.RoutePrefix = "api";
                cfg.SwaggerEndpoint($"{pathbase}/api/v1/openapi.json", $"{openapi.ApiName} (v1)");
                cfg.OAuthClientId(openapi.Client.ClientId);
                cfg.OAuthAppName(openapi.Client.ClientName ?? openapi.Client.ClientId);
                cfg.OAuthScopes(audience);
            });

            return app;
        }
    }
}
