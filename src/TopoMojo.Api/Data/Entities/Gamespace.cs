// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using TopoMojo.Api.Data.Abstractions;

namespace TopoMojo.Api.Data
{
    public class Gamespace : IEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ManagerId { get; set; }
        public string ManagerName { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public DateTimeOffset ExpirationTime { get; set; }
        public int CleanupGraceMinutes { get; set; }
        public bool Cleaned { get; set; }
        public bool AllowReset { get; set; }
        public string Challenge { get; set; }
        public string WorkspaceId { get; set; }
        public int Variant { get; set; }
        public int PlayerCount { get; set; }
        public virtual Workspace Workspace { get; set; }
        public virtual ICollection<Player> Players { get; set; } = new List<Player>();
        [NotMapped] public bool HasStarted => StartTime.Year > 1;
        [NotMapped] public bool HasEnded => EndTime.Year > 1;
        [NotMapped] public bool IsExpired => ExpirationTime < DateTimeOffset.UtcNow;
        [NotMapped] public bool IsActive => HasStarted && !HasEnded && !IsExpired;
    }

}
