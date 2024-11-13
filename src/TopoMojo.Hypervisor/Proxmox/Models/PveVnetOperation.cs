// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;

namespace TopoMojo.Hypervisor.Proxmox.Models
{
    internal sealed class PveVnetOperation(string networkName, PveVnetOperationType type) : IEquatable<PveVnetOperation>
    {
        public PveVnetOperationType Type { get; private set; } = type;
        public string NetworkName { get; private set; } = networkName;

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(PveVnetOperation))
                return false;

            var typedObj = obj as PveVnetOperation;

            return Equals(typedObj);
        }

        public bool Equals(PveVnetOperation other)
        {
            return
                other.NetworkName == this.NetworkName &&
                other.Type == this.Type;
        }

        public override int GetHashCode()
            => (NetworkName + Type.ToString()).GetHashCode();
    }
}
