// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;

namespace TopoMojo.Api
{
    public class UserClaimsTransformation
    (
        IMemoryCache cache,
        ILogger<UserClaimsTransformation> logger,
        OidcOptions oidcOptions,
        UserService svc
    ) : IClaimsTransformation
    {
        private readonly IMemoryCache _cache = cache;
        private readonly TimeSpan _cacheTimeout = new(0, 2, 0);
        private readonly UserService _svc = svc;

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            var user = await ResolveUser(principal);

            var claims = new Claim[]
            {
                new(AppConstants.SubjectClaimName, user.Id),
                new(AppConstants.NameClaimName, user.Name ?? ""),
                new(AppConstants.UserScopeClaim, user.Scope ?? ""),
                new(AppConstants.UserWorkspaceLimitClaim, user.WorkspaceLimit.ToString()),
                new(AppConstants.UserGamespaceLimitClaim, user.GamespaceLimit.ToString()),
                new(AppConstants.UserGamespaceMaxMinutesClaim, user.GamespaceMaxMinutes.ToString()),
                new(AppConstants.UserGamespaceCleanupGraceMinutesClaim, user.GamespaceCleanupGraceMinutes.ToString()),
                new(AppConstants.RoleClaimName, user.Role.ToString()),
            };

            return new ClaimsPrincipal(
                new ClaimsIdentity(
                    claims,
                    principal.Identity.AuthenticationType,
                    AppConstants.NameClaimName,
                    AppConstants.RoleClaimName
                )
            );
        }

        /// <summary>
        /// Determines if the current ClaimsPrincipal contains a claim that indicates that it is attempting to auth as a
        /// service account (via OAuth Client Credentials).
        /// </summary>
        /// <param name="principal"></param>
        /// <returns>The clientID of the OAuth client eligible for auth if configured correctly, or null if not.</returns>
        private string ResolveServiceAccountClientId(ClaimsPrincipal principal)
        {
            var serviceAccountClientId = string.Empty;

            // if authenticating as a service account, appropriate OIDC options (AuthTypeClaimName and ServiceAccountAuthType) must be configured
            // and the token must have the appropriate claim and value
            if (oidcOptions.AuthTypeClaimName.IsEmpty())
            {
                logger.LogInformation("Auth type claim not configured.");
                return null;
            }

            var claim = principal.Claims.FirstOrDefault(c => c.Type == oidcOptions.AuthTypeClaimName);
            if (claim is null)
            {
                logger.LogInformation("Auth type claim {claimName} not present.", oidcOptions.AuthTypeClaimName);
                return null;
            }

            if (claim.Value != oidcOptions.ServiceAccountAuthType)
            {
                logger.LogInformation("Auth type claim {claimName} has unexpected value {claimValue}", claim.Type, claim.Value);
                return null;
            }

            var clientIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "client_id" || c.Type == "azp");
            if (clientIdClaim is null)
            {
                logger.LogInformation("Couldn't resolve a client ID from the ClaimsPrincipal.");
                return null;
            }

            return clientIdClaim.Value;
        }

        /// <summary>
        /// This transformation supports two paths to resolution of a user:
        ///     1. A typical subject-claim based user identification
        ///     2. A claim that identifies the token as belong to a service account which matches an existing user's ServiceAccountClientId property.
        /// 
        /// The claim identifying service accounts will be consulted first if it is configured in Topo's OIDC settings and present in the claims principal. Otherwise,
        /// this function falls back to the subject claim to match to a user.
        /// </summary>
        /// <param name="principal"></param>
        /// <returns>A Topomojo User representing the result of the resolution process.</returns>
        private async Task<User> ResolveUser(ClaimsPrincipal principal)
        {
            var subject = principal.Subject();
            var serviceAccountClientId = ResolveServiceAccountClientId(principal);
            var resolvedUser = default(User);

            if (subject.IsEmpty() && !serviceAccountClientId.IsEmpty())
            {
                throw new ArgumentException($"""Can't resolve user: ClaimsPrincipal requires a "subject" claim or must have the claim {oidcOptions.AuthTypeClaimName} with value {oidcOptions.ServiceAccountAuthType}.""");
            }

            // first attempt to auth by service account, then by subject
            if (serviceAccountClientId.NotEmpty())
            {
                if (!_cache.TryGetValue(serviceAccountClientId, out User serviceAccountUser))
                {
                    serviceAccountUser = await _svc.FindByServiceAccountClientId(serviceAccountClientId) ?? throw new Exception($"Service account client ID {serviceAccountClientId} didn't resolve to a user.");

                    if (serviceAccountUser is null)
                    {
                        throw new UserServiceAccountResolutionFailed(serviceAccountClientId);
                    }

                    _cache.Set(serviceAccountClientId, serviceAccountUser, _cacheTimeout);
                }

                logger.LogInformation("Resolved user {userId} with service account ID {serviceAccountId}", serviceAccountUser.Id, serviceAccountClientId);
                resolvedUser = serviceAccountUser;
            }
            else if (subject.NotEmpty())
            {
                if (!_cache.TryGetValue<User>(subject, out User user))
                {
                    user = await _svc.Load(subject) ?? new Models.User
                    {
                        Id = subject,
                        Name = principal.Name()
                    };

                    // TODO: implement IChangeToken for this
                    _cache.Set<User>(subject, user, _cacheTimeout);
                }

                logger.LogInformation("Resolved user {userId} via subject claim", user.Id);
                resolvedUser = user;
            }

            if (resolvedUser is null)
            {
                throw new UserResolutionFailed();
            }
            else if (resolvedUser.Role == UserRole.Disabled)
            {
                throw new UserDisabled();
            }

            logger.LogInformation("Resolved user {userId} for claims transformation", resolvedUser.Id);
            return resolvedUser;
        }
    }
}
