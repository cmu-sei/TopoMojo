// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Models
{
    public class Search
    {
        public string Term { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
        public string Sort { get; set; }
        public string[] Filter { get; set; } = new string[] {};
    }

}
