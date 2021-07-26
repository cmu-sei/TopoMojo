// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using TopoMojo.Api.Models;

namespace TopoMojo.Api
{
    public static class ClaimsPrincipalExtensions
    {

        public static Models.User ToModel(this ClaimsPrincipal principal)
        {
            return new Models.User
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
