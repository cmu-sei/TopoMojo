// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Data
{
    public class User : IEntity
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Scope { get; set; }
        public int WorkspaceLimit { get; set; }
        public int GamespaceLimit { get; set; }
        public int GamespaceMaxMinutes { get; set; }
        public int GamespaceCleanupGraceMinutes { get; set; }
        public UserRole Role { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
        public ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
    }
}
