// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;

namespace TopoMojo.Hypervisor
{
    public class HypervisorServiceConfiguration
    {
        public bool IsVCenter { get; set; }
        public HypervisorType HypervisorType { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public string Host { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string AccessToken { get; set; }
        public string PoolPath { get; set; }
        public string Uplink { get; set; } = "dvs-topomojo";
        public string VmStore { get; set; } = "[topomojo] _run/";
        public string DiskStore { get; set; } = "[topomojo]";
        public string IsoStore { get; set; } = "[topomojo] iso/";
        public string TicketUrlHandler { get; set; } = "querystring"; //"local-app", "external-domain", "host-map", "none"
        public Dictionary<string, string> TicketUrlHostMap { get; set; } = [];
        public VlanConfiguration Vlan { get; set; } = new VlanConfiguration();
        public int KeepAliveMinutes { get; set; } = 10;
        public string ExcludeNetworkMask { get; set; } = "topomojo";
        public string Tenant { get; set; } = "";
        public bool IsNsxNetwork { get; set; }
        public bool DebugVerbose { get; set; }
        public bool IgnoreCertificateErrors { get; set; }
        public string SDNZone { get; set; } = "topomojo";

        /// <summary>
        /// Proxmox only. Register deployed vms as cluster HA resources, which is what allows
        /// Proxmox's Cluster Resource Scheduler (CRS) to place and rebalance them.
        /// Requires PVE 9 and shared vm storage. See docs/Proxmox.md.
        /// </summary>
        public bool EnableHA { get; set; }

        /// <summary>
        /// Proxmox only. Fail a deployment when a vm cannot be registered as an HA resource, rather
        /// than deploying it un-managed. Has no effect unless EnableHA is set.
        /// </summary>
        public bool RequireHA { get; set; }

        /// <summary>
        /// Proxmox only. Allow CRS to migrate HA managed vms during automatic rebalancing.
        /// Has no effect unless EnableHA is set.
        /// </summary>
        public bool HaAutoRebalance { get; set; } = true;

        /// <summary>
        /// Proxmox only. Maximum tries to restart an HA resource on a node after a failed start.
        /// Null uses the Proxmox default. Has no effect unless EnableHA is set.
        /// </summary>
        public int? HaMaxRestart { get; set; }

        /// <summary>
        /// Proxmox only. Maximum tries to relocate an HA resource that fails to start.
        /// Null uses the Proxmox default. Has no effect unless EnableHA is set.
        /// </summary>
        public int? HaMaxRelocate { get; set; }
        /// <summary>
        /// Proxmox only. Timeout for an ISO push through PVE's storage upload API. Populated from
        /// FileUpload__UploadTimeoutMinutes, the same setting the vSphere datastore upload uses;
        /// it has to live here because FileUploadOptions is declared in TopoMojo.Api, which
        /// TopoMojo.Hypervisor cannot reference.
        /// </summary>
        public int IsoUploadTimeoutMinutes { get; set; } = 120;

        public SddcConfiguration Sddc { get; set; } = new SddcConfiguration();
    }

    public class SddcConfiguration
    {
        public string ApiUrl { get; set; }
        public string MetadataUrl { get; set; }
        public string SegmentApiPath { get; set; } = "policy/api/v1/infra/tier-1s/cgw/segments";
        public string AuthUrl { get; set; }
        public string AuthTokenHeader { get; set; } = "csp-auth-token";
        public string OrgId { get; set; }
        public string SddcId { get; set; }
        public string ApiKey { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
    }

    public class VlanConfiguration
    {
        public string Range { get; set; } = "";
        public Vlan[] Reservations { get; set; } = [];
        public int ResetDebounceDuration { get; set; } = 2000;
        public int? ResetDebounceMaxDuration { get; set; } = 5000;
    }

    public class Vlan
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool OnUplink { get; set; }
    }

}
