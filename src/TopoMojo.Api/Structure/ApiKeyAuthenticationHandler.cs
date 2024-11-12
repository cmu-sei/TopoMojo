// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace TopoMojo.Api
{
    public static class ApiKeyAuthentication
    {
        public const string AuthenticationScheme = "ApiKey";
        public const string ApiKeyHeaderName = "x-api-key";
        public const string AuthorizationHeaderName = "Authorization";
        public const string ChallengeHeaderName = "WWW-Authenticate";

        public static class ClaimNames
        {
            public const string Subject = AppConstants.SubjectClaimName;
            public const string Name = AppConstants.NameClaimName;
        }

    }
    public class ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyAuthenticationService svc
        ) : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
    {
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            await Task.Delay(0);

            string key = Request.Headers[ApiKeyAuthentication.ApiKeyHeaderName];
            string name = "";

            if (string.IsNullOrEmpty(key))
            {
                string[] authHeader = Request.Headers[ApiKeyAuthentication.AuthorizationHeaderName]
                    .ToString()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);

                if (authHeader?.Length > 0)
                {
                    string scheme = authHeader[0];

                    if (authHeader.Length > 1
                        && scheme.Equals(ApiKeyAuthentication.AuthenticationScheme, StringComparison.OrdinalIgnoreCase)
                    ) {
                        key = authHeader[1];

                        if (authHeader.Length > 2)
                            name = authHeader[2];
                    }
                }
            }

            var subject = await svc.ResolveApiKey(key);

            if (subject is null)
                return AuthenticateResult.NoResult();

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new Claim[] {
                        new(ApiKeyAuthentication.ClaimNames.Subject, subject.Id),
                        new(ApiKeyAuthentication.ClaimNames.Name, name ?? subject.Name)
                    },
                    Scheme.Name
                )
            );

            return AuthenticateResult.Success(
                new AuthenticationTicket(principal, Scheme.Name)
            );
        }

        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.Headers[ApiKeyAuthentication.ChallengeHeaderName] = ApiKeyAuthentication.AuthenticationScheme;

            await base.HandleChallengeAsync(properties);
        }
    }

    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
    }

    public class ApiKeyResolvedUser
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    public interface IApiKeyAuthenticationService
    {
        Task<ApiKeyResolvedUser> ResolveApiKey(string key);
    }

}
