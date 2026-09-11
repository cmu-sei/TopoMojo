// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;

namespace TopoMojo.Hypervisor.Proxmox;

internal static class ProxmoxVmActivity
{
    public static VmActivity Resolve(Vm vm, ClusterHaStatusCurrent ha, VmActivity requested)
    {
        var state = ha?.CrmState ?? ha?.State;
        if (state == "error")
            return new() { Kind = VmActivityKind.Busy, Status = VmActivityStatus.Failed,
                Message = "The hypervisor could not complete the VM operation." };

        if (state is "migrate" or "relocate" || ha?.RequestState is "migrate" or "relocate")
            return new() { Kind = VmActivityKind.Migrating };

        var task = VmActivity.FromTask(vm.Task);
        if (task != null)
            return task;

        if (requested?.Status == VmActivityStatus.Failed)
            return requested;

        // HA may accept a start before the LRM has started (or relocated) the guest.
        if (vm.State != VmPowerState.Running &&
            (requested != null || ha?.RequestState == "started" || state == "started"))
            return new() { Kind = VmActivityKind.Starting };

        if (vm.State == VmPowerState.Running && ha?.RequestState == "stopped")
            return new() { Kind = VmActivityKind.Busy };

        // Fencing/recovery and future transitional states are activity, not idle/off.
        if (state is not (null or "" or "started" or "stopped" or "disabled" or "ignored"))
            return new() { Kind = VmActivityKind.Busy };

        return null;
    }
}
