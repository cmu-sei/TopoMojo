// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Hubs;

public class BroadcastEvent
{
    public BroadcastEvent(
        System.Security.Principal.IPrincipal user,
        string action
    )
    {
        Actor = user.AsActor();
        Action = action;
    }

    public Actor Actor { get; private set; }
    public string Action { get; set; }
}

public class BroadcastEvent<T> : BroadcastEvent where T : class
{
    public BroadcastEvent(
        System.Security.Principal.IPrincipal user,
        string action,
        T model
    ) : base(user, action)
    {
        Model = model;
    }

    public T Model { get; set; }
}
