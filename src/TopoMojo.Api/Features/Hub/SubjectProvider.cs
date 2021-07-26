// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace TopoMojo.Api.Hubs
{
    public class SubjectProvider : IUserIdProvider
    {
        public virtual string GetUserId(HubConnectionContext connection)
        {
            return connection.User.FindFirstValue(AppConstants.SubjectClaimName);
        }
    }
}
