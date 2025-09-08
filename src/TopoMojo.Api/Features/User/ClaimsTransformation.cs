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
        /// This transformation supports two paths to resolution of a user:
        ///     1. A typical subject-claim based user identification
        ///     2. A client_id claim that matches the ServiceAccountClientId of a Topomojo user. (You can configure Topo to look for a different claim name using Oidc__ServiceAccountClientIdClaimType.)
        /// 
        /// If the client_id claim is present, we attempt to match it to a user's ServiceAccountClientId and throw if this fails (because if the client_id is claim is present, we assume you're logging in as a service account.) 
        /// If it is not, we perform typical auth. 
        /// </summary>
        /// <param name="principal"></param>
        /// <returns>A Topomojo User representing the result of the resolution process.</returns>
        private async Task<User> ResolveUser(ClaimsPrincipal principal)
        {
            var subject = principal.Subject();
            var resolvedUser = default(User);

            // if Oidc__ServiceAccountClientIdClaimType is empty, don't resolve a service account ID to disable this kind of auth
            string serviceAccountClientId = null;
            // TM doesn't have a way to set config values to null by configuration file, so we check for the explicit string "null"
            if (oidcOptions.ServiceAccountClientIdClaimType.NotEmpty() && oidcOptions.ServiceAccountClientIdClaimType != "null")
            {
                serviceAccountClientId = principal.Claims.FirstOrDefault(c => c.Type == oidcOptions.ServiceAccountClientIdClaimType)?.Value;
            }

            if (serviceAccountClientId.NotEmpty())
            {
                if (!_cache.TryGetValue(serviceAccountClientId, out User serviceAccountUser))
                {
                    serviceAccountUser = await _svc.FindByServiceAccountClientId(serviceAccountClientId) ?? throw new UserServiceAccountResolutionFailed(serviceAccountClientId);
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

            if (resolvedUser == default(User))
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
