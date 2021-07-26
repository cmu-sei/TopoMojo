// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using TopoMojo.Api.Data.Abstractions;

namespace TopoMojo.Api.Data
{
    public class Workspace : IEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Audience { get; set; }
        public string ShareCode { get; set; }
        public string TemplateScope { get; set; }
        public bool IsPublished { get; set; }
        public int TemplateLimit { get; set; }
        public bool HostAffinity { get; set; }
        public bool UseUplinkSwitch { get; set; }
        public int DurationMinutes { get; set; }
        public int LaunchCount { get; set; }
        public DateTimeOffset LastActivity { get; set; }
        public string Challenge { get; set; }
        public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
        public virtual ICollection<Gamespace> Gamespaces { get; set; } = new List<Gamespace>();
        public virtual ICollection<Template> Templates { get; set; } = new List<Template>();

    }
}
