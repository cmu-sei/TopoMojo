// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using TopoMojo.Hypervisor.Extensions;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ProxmoxVmObservationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void FreshDeploymentRetainsIdentityWhilePowerAndNodeConverge(string name)
    {
        var deployed = new Vm
        {
            Id = "123", Name = "Ubuntu#workspace-id", Host = "pve1", State = VmPowerState.Off
        };
        var observed = new ClusterResource
        {
            ResourceType = ClusterResourceType.Vm, VmId = 123, Node = "pve2", IsRunning = true
        };

        var vm = ProxmoxClient.MapVmObservation(observed, name, deployed);

        Assert.Equal("Ubuntu#workspace-id", vm.Name);
        Assert.Equal("workspace-id", vm.Name.Tag());
        Assert.Equal("pve2", vm.Host);
        Assert.Equal(VmPowerState.Running, vm.State);
        Assert.Equal("Ubuntu#workspace-id", deployed.Name);
    }

    [Fact]
    public void FreshNameReplacesPreviousNameWhenInventoryConverges()
    {
        var vm = ProxmoxClient.MapVmObservation(
            new ClusterResource { VmId = 123, IsRunning = false },
            "Renamed#workspace-id",
            new Vm { Id = "123", Name = "Previous#workspace-id" });
        Assert.Equal("Renamed#workspace-id", vm.Name);
        Assert.Equal(VmPowerState.Off, vm.State);
    }

    [Fact]
    public void MissingNameDoesNotBorrowIdentityFromAnotherVm()
    {
        var vm = ProxmoxClient.MapVmObservation(
            new ClusterResource { VmId = 123 }, null,
            new Vm { Id = "456", Name = "Other#other-workspace" });
        Assert.Equal("", vm.Name);
    }
}
