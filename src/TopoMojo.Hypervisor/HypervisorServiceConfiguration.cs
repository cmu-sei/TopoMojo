// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;

namespace TopoMojo.Hypervisor
{
    public class HypervisorServiceConfiguration
    {
        public bool IsVCenter { get; set; }
        public bool IsProxmox { get; set; }
        public string Type { get; set; }
        public string Url { get; set; }
        public string Host { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string PoolPath { get; set; }
        public string Uplink { get; set; } = "dvs-topomojo";
        public string VmStore { get; set; } = "[topomojo] _run/";
        public string DiskStore { get; set; } = "[topomojo]";
        public string IsoStore { get; set; } = "[topomojo] iso/";
        public string TicketUrlHandler { get; set; } = "querystring"; //"local-app", "external-domain", "host-map", "none"
        public Dictionary<string, string> TicketUrlHostMap { get; set; } = new Dictionary<string, string>();
        public VlanConfiguration Vlan { get; set; } = new VlanConfiguration();
        public int KeepAliveMinutes { get; set; } = 10;
        public string ExcludeNetworkMask { get; set; } = "topomojo";
        public string Tenant { get; set; } = "";
        public bool IsNsxNetwork { get; set; }
        public bool DebugVerbose { get; set; }
        public bool IgnoreCertificateErrors { get; set; }
        public string SDNZone { get; set; } = "topomojo";
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
        public Vlan[] Reservations { get; set; } = new Vlan[] { };
    }

    public class Vlan
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool OnUplink { get; set; }
    }

}
