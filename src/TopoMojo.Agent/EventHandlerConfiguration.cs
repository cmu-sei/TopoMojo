// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Agent;

public class EventHandlerConfiguration
{
    public string Url { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string HeartbeatTrigger { get; set; } = "";
    public int HeartbeatSeconds { get; set; } = 10;
    public bool QuietLogging { get; set; }

    public bool IsValid =>
        Uri.TryCreate(Url, UriKind.Absolute, out _) &&
        !string.IsNullOrEmpty(ApiKey) &&
        !string.IsNullOrEmpty(GroupId)
    ;
}
