// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;

namespace TopoMojo
{
    [Flags]
    public enum Permission {
        None =      0,
        Editor =    0x01,
        Manager =   0xff
    }
}
