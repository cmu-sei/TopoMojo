// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System.Security.Claims;
using TopoMojo.Api.Models;

namespace TopoMojo.Api
{
    public static class ClaimsPrincipalExtensions
    {

        public static User ToModel(this ClaimsPrincipal principal)
        {
            string sub = principal.Subject();

            // support anonymous endpoints
            if (string.IsNullOrEmpty(sub))
            {
                return new User
                {
                    Role = UserRole.User
                };
            }

            return new User
            {
                Id = principal.Subject(),

                Name = principal.Name(),

                Scope = principal.FindFirstValue(AppConstants.UserScopeClaim),

                WorkspaceLimit = int.Parse(
                    principal.FindFirstValue(AppConstants.UserWorkspaceLimitClaim) ?? "0"
                ),

                GamespaceLimit = int.Parse(
                    principal.FindFirstValue(AppConstants.UserGamespaceLimitClaim) ?? "0"
                ),

                GamespaceMaxMinutes = int.Parse(
                    principal.FindFirstValue(AppConstants.UserGamespaceMaxMinutesClaim) ?? "0"
                ),

                GamespaceCleanupGraceMinutes = int.Parse(
                    principal.FindFirstValue(AppConstants.UserGamespaceCleanupGraceMinutesClaim) ?? "0"
                ),

                Role = Enum.Parse<UserRole>(
                    string.Join(',',
                    principal.FindAll(AppConstants.RoleClaimName)
                        .Select(c => c.Value)
                        .ToArray()
                    )
                )
            };
        }

        public static string Subject(this ClaimsPrincipal user)
        {
            return
                user.FindFirstValue(AppConstants.SubjectClaimName) ??
                user.FindFirstValue(AppConstants.ClientIdClaimName)
            ;
        }

        public static string Name(this ClaimsPrincipal user)
        {
            return
                user.FindFirstValue(AppConstants.NameClaimName)
            ;
        }

    }
}
