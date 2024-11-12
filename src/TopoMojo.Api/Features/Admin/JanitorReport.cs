// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

namespace TopoMojo.Api.Models
{
    public class JanitorReport
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Reason { get; set; }
        public DateTimeOffset Age { get; set; }
    }
}
