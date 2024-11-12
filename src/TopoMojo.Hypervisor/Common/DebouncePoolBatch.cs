// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;

namespace TopoMojo.Hypervisor
{
    public sealed class DebouncePoolBatch<T>
    {
        public string Id { get; set; }
        public IEnumerable<T> Items { get; set; }
    }
}
