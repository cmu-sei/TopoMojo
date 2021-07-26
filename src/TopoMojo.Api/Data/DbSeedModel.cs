// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Models
{
    public class DbSeedModel
    {
        public DbSeedUser[] Users { get; set; } = new DbSeedUser[] {};
    }

    public class DbSeedUser
    {
        public string Name { get; set; }
        public string GlobalId { get; set; }
        public bool IsAdmin { get; set; }
    }
}
