// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using TopoMojo.Api.Data.Abstractions;

namespace TopoMojo.Api.Data
{
    public class Template : IEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public System.DateTimeOffset WhenCreated { get; set; }
        public string Description { get; set; }
        public string Audience { get; set; }
        public string Iso { get; set; }
        public string Networks { get; set; }
        public string Guestinfo { get; set; }
        public bool IsHidden { get; set; }
        public bool IsPublished { get; set; }
        public bool IsLinked { get; set; }
        public string Detail { get; set; }
        public string ParentId { get; set; }
        public int Replicas { get; set; }
        public int Variant { get; set; }
        public virtual Template Parent { get; set; }
        public string WorkspaceId { get; set; }
        public virtual Workspace Workspace { get; set; }
    }
}
