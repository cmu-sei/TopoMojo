// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;

namespace TopoMojo.Api
{
    public class UserClaimsTransformation: IClaimsTransformation
    {
        private readonly IMemoryCache _cache;
        private readonly UserService _svc;

        public UserClaimsTransformation(
            IMemoryCache cache,
            UserService svc
        )
        {
            _cache = cache;
            _svc = svc;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {

            string subject = principal.Subject()
                ?? throw new ArgumentException("ClaimsPrincipal requires 'sub' claim");

            if (! _cache.TryGetValue<User>(subject, out User user))
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
                new Claim(AppConstants.SubjectClaimName, user.Id),
                new Claim(AppConstants.NameClaimName, user.Name ?? ""),
                new Claim(AppConstants.UserScopeClaim, user.Scope ?? ""),
                new Claim(AppConstants.UserWorkspaceLimitClaim, user.WorkspaceLimit.ToString()),
                new Claim(AppConstants.UserGamespaceLimitClaim, user.GamespaceLimit.ToString()),
                new Claim(AppConstants.UserGamespaceMaxMinutesClaim, user.GamespaceMaxMinutes.ToString()),
                new Claim(AppConstants.UserGamespaceCleanupGraceMinutesClaim, user.GamespaceCleanupGraceMinutes.ToString()),
                new Claim(AppConstants.RoleClaimName, user.Role.ToString()),
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
