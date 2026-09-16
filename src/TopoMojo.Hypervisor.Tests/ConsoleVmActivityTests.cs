using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ConsoleVmActivityTests
{
    [Fact]
    public void StartRemainsActiveUntilObservedRunning()
    {
        var vm = new Vm { State = VmPowerState.Off };
        Assert.Null(ProxmoxVmActivity.Resolve(vm, null, null));
        var pending = new VmActivity { Kind = VmActivityKind.Starting };
        Assert.Equal(VmActivityKind.Starting, ProxmoxVmActivity.Resolve(vm, null, pending).Kind);
        // A new tab can discover the operation without a local pending request.
        var ha = new ClusterHaStatusCurrent { CrmState = "started" };
        Assert.Equal(VmActivityKind.Starting, ProxmoxVmActivity.Resolve(vm, ha, null).Kind);
        vm.State = VmPowerState.Running;
        Assert.Null(ProxmoxVmActivity.Resolve(vm, ha, null));
    }

    [Theory]
    [InlineData("migrate", null)]
    [InlineData("stopped", "relocate")]
    public void HaMigrationOverridesGenericTaskAndPendingStart(string state, string request)
    {
        var activity = ProxmoxVmActivity.Resolve(
            new() { State = VmPowerState.Off, Task = new() { Name = "saving", Progress = 50 } },
            new() { CrmState = state, RequestState = request },
            new() { Kind = VmActivityKind.Starting });
        Assert.Equal(VmActivityKind.Migrating, activity.Kind);
        Assert.Equal(VmActivityStatus.Active, activity.Status);
    }

    [Theory]
    [InlineData("error", VmActivityStatus.Failed)]
    [InlineData("fence", VmActivityStatus.Active)]
    public void HaFailureAndRecoveryAreNotIdle(string state, VmActivityStatus status)
    {
        var activity = ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off },
            new() { CrmState = state }, null);
        Assert.Equal(status, activity.Status);
    }

    [Theory]
    [InlineData(99, VmActivityStatus.Active)]
    [InlineData(-1, VmActivityStatus.Failed)]
    [InlineData(100, null)]
    public void GenericTasksUseProgressNotDisplayText(int progress, VmActivityStatus? status)
    {
        var activity = VmActivity.FromTask(new() { Name = "migrating", Progress = progress });
        Assert.Equal(status, activity?.Status);
        if (activity != null) Assert.Equal(VmActivityKind.Busy, activity.Kind);
    }

    [Fact]
    public void FailedLocalStartSurvivesAnOffRead()
    {
        var failed = new VmActivity { Kind = VmActivityKind.Starting, Status = VmActivityStatus.Failed };
        Assert.Same(failed, ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off }, null, failed));
    }
}
