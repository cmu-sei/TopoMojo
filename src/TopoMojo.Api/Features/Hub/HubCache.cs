// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Concurrent;

namespace TopoMojo.Api.Hubs
{
    public class HubCache
    {
        public ConcurrentDictionary<string,CachedConnection> Connections { get; } = new ConcurrentDictionary<string,CachedConnection>();
    }

    public class CachedConnection
    {
        public string Id { get; set; }
        public string Room { get; set; }
        public string ProfileId { get; set; }
        public string ProfileName { get; set; }
    }

}
