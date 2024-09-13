// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
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
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private readonly IApiKeyAuthenticationService _svc;

        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IApiKeyAuthenticationService svc
        )
            : base(options, logger, encoder)
        {
            _svc = svc;
        }

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

                if (authHeader?.Any() == true)
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

            var subject = await _svc.ResolveApiKey(key);

            if (subject is null)
                return AuthenticateResult.NoResult();

            var principal = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new Claim[] {
                        new Claim(ApiKeyAuthentication.ClaimNames.Subject, subject.Id),
                        new Claim(ApiKeyAuthentication.ClaimNames.Name, name ?? subject.Name)
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
