// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Hubs;

public class BroadcastEvent(
    System.Security.Principal.IPrincipal user,
    string action
    )
{
    public Actor Actor { get; private set; } = user.AsActor();
    public string Action { get; set; } = action;
}

public class BroadcastEvent<T>(
    System.Security.Principal.IPrincipal user,
    string action,
    T model
    ) : BroadcastEvent(user, action) where T : class
{
    public T Model { get; set; } = model;
}
