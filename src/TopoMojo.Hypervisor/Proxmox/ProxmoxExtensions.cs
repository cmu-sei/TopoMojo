using System;
using System.Linq;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Node;

namespace TopoMojo.Hypervisor.Proxmox
{
    public static class ProxmoxExtensions
    {
        public static long GetId(this Vm vm)
        {
            return long.Parse(vm.Id);
        }

        public static string GetParentFilename(this NodeStorageContent content)
        {
            if (string.IsNullOrEmpty(content.Parent))
            {
                return null;
            }

            // raw format parent: base-<vmId>-disk-<disk_num>@__base__
            if (content.Parent.Contains('@'))
            {
                // return e.g. base-100-disk-0
                return content.Parent.Split('@')[0];
            }
            // qcow2 format: ../<vmId>/base-<vmId>-disk-<disk_num>.qcow2
            else if (content.Parent.StartsWith("../"))
            {
                // return e.g. 100/base-100-disk0.qcow2
                return content.Parent.Split(new[] { '/' }, 2)[1];
            }
            else
            {
                throw new InvalidOperationException("Unsupported NodeStorageContent type");
            }
        }
    }
}