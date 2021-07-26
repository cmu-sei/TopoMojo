// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor.vSphere
{
    public class VmContext
    {
        public Vm Vm { get; set; }
        public VimClient Host { get; set; }
    }
}
