using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ConsoleVmActivityTests
{
    [Fact]
    public void IdleOffVmHasNoActivity()
        => Assert.Null(ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off }, null, null));

    [Fact]
    public void AcceptedHaStartRemainsStartingWhileGuestIsOff()
    {
        var activity = ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off },
            new() { CrmState = "stopped", RequestState = "started" }, null);
        Assert.Equal(VmActivityKind.Starting, activity.Kind);
        Assert.Equal(VmActivityStatus.Active, activity.Status);
    }

    [Theory]
    [InlineData("migrate")]
    [InlineData("relocate")]
    public void MigrationTakesPrecedenceOverPendingStart(string state)
    {
        var activity = ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off },
            new() { CrmState = state }, new() { Kind = VmActivityKind.Starting });
        Assert.Equal(VmActivityKind.Migrating, activity.Kind);
    }

    [Fact]
    public void HaActivityIsDiscoverableWithoutALocalStartRequest()
    {
        var activity = ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off },
            new() { CrmState = "started" }, null);
        Assert.Equal(VmActivityKind.Starting, activity.Kind);
    }

    [Fact]
    public void RunningGuestCompletesStartingActivity()
        => Assert.Null(ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Running },
            new() { CrmState = "started", RequestState = "started" }, null));

    [Fact]
    public void HaFailureIsNotIndefiniteProgress()
    {
        var activity = ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off },
            new() { CrmState = "error" }, new() { Kind = VmActivityKind.Starting });
        Assert.Equal(VmActivityStatus.Failed, activity.Status);
        Assert.NotEmpty(activity.Message);
    }

    [Theory]
    [InlineData(0, VmActivityStatus.Active)]
    [InlineData(99, VmActivityStatus.Active)]
    [InlineData(-1, VmActivityStatus.Failed)]
    public void ExistingTasksExposeActivityAndFailure(int progress, VmActivityStatus status)
        => Assert.Equal(status, VmActivity.FromTask(new() { Name = "saving", Progress = progress }).Status);

    [Fact]
    public void CompletedTaskDoesNotBlockConsole()
        => Assert.Null(VmActivity.FromTask(new() { Progress = 100 }));

    [Fact]
    public void FailedLocalStartSurvivesAnOffRead()
    {
        var failed = new VmActivity { Kind = VmActivityKind.Starting, Status = VmActivityStatus.Failed };
        Assert.Same(failed, ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off }, null, failed));
    }

    [Fact]
    public void FencingIsAnActiveOperationRatherThanAFailedStart()
    {
        var activity = ProxmoxVmActivity.Resolve(new() { State = VmPowerState.Off },
            new() { CrmState = "fence" }, null);
        Assert.Equal(VmActivityKind.Busy, activity.Kind);
        Assert.Equal(VmActivityStatus.Active, activity.Status);
    }
}
