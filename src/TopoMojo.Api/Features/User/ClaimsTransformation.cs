// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;

namespace TopoMojo.Api
{
    public class UserClaimsTransformation(
        IMemoryCache cache,
        UserService svc
        ) : IClaimsTransformation
    {
        private readonly IMemoryCache _cache = cache;
        private readonly UserService _svc = svc;

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {

            string subject = principal.Subject()
                ?? throw new ArgumentException("ClaimsPrincipal requires 'sub' claim");

            if (!_cache.TryGetValue<User>(subject, out User user))
            {
                user = await _svc.Load(subject) ?? new Models.User
                {
                    Id = subject,
                    Name = principal.Name()
                };

                // TODO: implement IChangeToken for this

                _cache.Set<User>(subject, user, new TimeSpan(0, 2, 0));
            }

            if (user.Role == UserRole.Disabled)
                throw new UserDisabled();

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
    }

}
