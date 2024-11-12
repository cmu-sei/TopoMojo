// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Hypervisor.Proxmox
{
    public interface IProxmoxNameService
    {
        string ToPveName(string name);
        string FromPveName(string pveName);
    }

    public class ProxmoxNameService : IProxmoxNameService
    {
        public static bool IsPveName(string name)
            => name.Contains("--");

        public string ToPveName(string name)
            => name.Replace("#", "--");

        public string FromPveName(string pveName)
            => pveName.Replace("--", "#");
    }
}
