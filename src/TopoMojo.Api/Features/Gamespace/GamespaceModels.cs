// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Models
{
    public class Gamespace
    {
        public string Id { get; set; }
        public string ManagerId { get; set; }
        public string ManagerName { get; set; }
        public string Audience { get; set; }
        public string Name { get; set; }
        public string Slug => Name.ToSlug();
        public bool IsActive { get; set; }
        public Player[] Players { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }

    }

    public class ChangedGamespace
    {
        public string Id { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }
    }

    public class GameState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Slug => Name.ToSlug();
        public string ManagerId { get; set; }
        public string ManagerName { get; set; }
        public string Markdown { get; set; }
        public string Audience { get; set; }
        public string LaunchpointUrl { get; set; }
        public Player[] Players { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }
        public bool IsActive { get; set; }
        public IEnumerable<VmState> Vms { get; set; } = new List<VmState>();
        public ChallengeView Challenge { get; set; }

    }

    public class GameStateSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public Player[] Players { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }

    }

    public class Player
    {
        public string GamespaceId { get; set; }
        public string SubjectId { get; set; }
        public string SubjectName { get; set; }
        public Permission Permission { get; set; }
        public bool IsManager => Permission == Permission.Manager;
    }

    public class Enlistee
    {
        public string SubjectName { get; set; }
        public string Code { get; set; }
    }

    public class Enlistment
    {
        public string GamespaceId { get; set; }
        public string Token { get; set; }
    }
    
    public class GamespaceSearch: Search
    {
        public const string FilterAll = "all";
        public const string FilterActive = "active";
        public bool WantsAll => Filter.Contains(FilterAll);
        public bool WantsActive => Filter.Contains(FilterActive);

    }

    public class GamespaceRegistration
    {
        public string ResourceId { get; set; }
        public string GraderKey { get; set; }
        public string GraderUrl { get; set; }
        public int Variant { get; set; }
        public int PlayerCount { get; set; }
        public int MaxAttempts { get; set; }
        public int MaxMinutes { get; set; }
        public int Points { get; set; } = 100;
        public bool AllowReset { get; set; }
        public bool AllowPreview { get; set; }
        public bool StartGamespace { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }
        public RegistrationPlayer[] Players { get; set; } = new RegistrationPlayer[] {};
    }

    public class RegistrationPlayer
    {
        public string SubjectId { get; set; }
        public string SubjectName { get; set; }
    }
}
