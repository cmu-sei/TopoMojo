// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace TopoMojo.Api.Hubs
{
    public static class HubExtensions
    {
        public static Actor AsActor(this System.Security.Principal.IPrincipal user)
        {
            var principal = user as ClaimsPrincipal;
            return new Actor
            {
                Id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub),
                Name = principal.FindFirstValue("name")
            };
        }
    }
}
