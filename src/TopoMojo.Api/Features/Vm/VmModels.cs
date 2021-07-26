// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api
{
    public class VmState
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string IsolationId { get; set; }
        public bool IsRunning { get; set; }
        public bool IsVisible { get; set; }
    }

}
