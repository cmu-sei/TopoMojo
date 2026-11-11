// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TopoMojo.Api.Controllers;
using TopoMojo.Api.Data;
using User = TopoMojo.Api.Models.User;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

/// <summary>
/// The room check on the two deploy paths: a workspace vm (VmController.DeployVm) and a
/// gamespace start or register (GamespaceService.Deploy), and how a refusal reaches the client.
/// </summary>
public sealed class DeployCapacityTests : IDisposable
{
    private const string WorkspaceId = "8f1d2c3b4a5e4f6a9b0c1d2e3f4a5b6c";
    private const string GamespaceId = "1a2b3c4d5e6f4a7b8c9d0e1f2a3b4c5d";

    private readonly DocRootFixture _fixture = new();
    private readonly DeployHypervisor _pod;
    private readonly IHypervisorService _podService;
    private readonly CoreOptions _options = new() { CapacityHeadroomPercent = 0 };
    private readonly CapacityGuard _guard;

    public DeployCapacityTests()
    {
        _podService = DeployHypervisor.Create(
            CapacityCheckTests.Capacity(null, CapacityCheckTests.Node("a", 10240, 0)));
        _pod = (DeployHypervisor)(object)_podService;
        _options.DocPath = _fixture.Root;
        _guard = new CapacityGuard(_podService, new MemoryCache(new MemoryCacheOptions()), _options, NullLogger<CapacityGuard>.Instance);
    }

    public void Dispose() => _fixture.Dispose();

    private static string Detail(string name, int ramGb) =>
        $$"""{ "name": "{{name}}", "ram": {{ramGb}}, "cpu": "1", "eth": [ { "net": "lan" } ], "disks": [] }""";

    private async Task AddTemplate(string id, int ramGb)
    {
        await _fixture.AddWorkspace(WorkspaceId);
        _fixture.Db.Templates.Add(new Data.Template
        {
            Id = id, Name = id, WorkspaceId = WorkspaceId,
            Detail = Detail(id, ramGb)
        });
        await _fixture.Db.SaveChangesAsync();
    }

    private TestVmController VmController(User actor) => new(
        _podService, _options, _guard,
        new TemplateService(new TemplateStore(_fixture.Db), _podService, NullLogger<TemplateService>.Instance, DocRootFixture.Mapper, _options),
        actor);

    private static User Author => new() { Id = WorkspaceId, Name = "author", Role = UserRole.Builder };
    private static User Admin => new() { Id = "admin", Name = "admin", Role = UserRole.Administrator };

    // ---- workspace vm deploy ----

    [Fact]
    public async Task VmDeploy_FitsWithoutAWarning()
    {
        await AddTemplate("small", 4);
        _options.CapacityCheck = CapacityCheckMode.Block;

        var vm = Deployed(await VmController(Author).DeployVm("small"));

        Assert.Null(vm.CapacityWarning);
        Assert.Equal(1, _pod.Deploys);
    }

    [Fact]
    public async Task VmDeploy_WarnDeploysAndCarriesTheWarning()
    {
        await AddTemplate("big", 16);
        _options.CapacityCheck = CapacityCheckMode.Warn;

        var vm = Deployed(await VmController(Author).DeployVm("big"));

        Assert.Contains("Not enough hypervisor memory", vm.CapacityWarning);
        Assert.Equal(1, _pod.Deploys);
        // the hypervisor's own vm object is left without the warning
        Assert.Null(_pod.Vms.Single().CapacityWarning);
    }

    [Fact]
    public async Task VmDeploy_BlockRefusesAnAuthor()
    {
        await AddTemplate("big", 16);
        _options.CapacityCheck = CapacityCheckMode.Block;

        await Assert.ThrowsAsync<CapacityExceeded>(() => VmController(Author).DeployVm("big"));
        Assert.Equal(0, _pod.Deploys);
    }

    [Fact]
    public async Task VmDeploy_BlockLetsAnAdminThroughWithTheWarning()
    {
        await AddTemplate("big", 16);
        _options.CapacityCheck = CapacityCheckMode.Block;

        var vm = Deployed(await VmController(Admin).DeployVm("big"));

        Assert.Contains("an administrator started it", vm.CapacityWarning);
    }

    [Fact]
    public async Task VmDeploy_AnAlreadyDeployedVmIsNotChecked()
    {
        await AddTemplate("big", 16);
        _pod.Vms.Add(new Vm { Id = "x", Name = $"big#{WorkspaceId}" });
        _options.CapacityCheck = CapacityCheckMode.Block;

        var result = await VmController(Author).DeployVm("big");

        Assert.Null(Deployed(result).CapacityWarning);
        Assert.Equal(0, _pod.CapacityReads);
    }

    [Fact]
    public async Task VmDeploy_AFailedDeployGivesItsMemoryBack()
    {
        await AddTemplate("eight", 8);
        _options.CapacityCheck = CapacityCheckMode.Block;
        _pod.FailDeploy = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => VmController(Author).DeployVm("eight"));

        Assert.Equal(0, _guard.ReservedMb(DateTimeOffset.MinValue));
    }

    private static Vm Deployed(ActionResult<Vm> result) => (Vm)Assert.IsType<OkObjectResult>(result.Result).Value;

    // ---- gamespace deploy ----

    private async Task AddGamespace(params (string name, int ramGb)[] templates)
    {
        await _fixture.AddWorkspace(WorkspaceId);
        foreach (var (name, ram) in templates)
        {
            _fixture.Db.Templates.Add(new Data.Template
            {
                Id = name, Name = name, WorkspaceId = WorkspaceId,
                Detail = Detail(name, ram)
            });
        }
        _fixture.Db.Gamespaces.Add(new Data.Gamespace
        {
            Id = GamespaceId, Name = "gs", WorkspaceId = WorkspaceId, Challenge = "{}",
            StartTime = DateTimeOffset.UtcNow, ExpirationTime = DateTimeOffset.UtcNow.AddHours(1)
        });
        await _fixture.Db.SaveChangesAsync();
    }

    private GamespaceService GamespaceService() => new(
        NullLogger<GamespaceService>.Instance,
        DocRootFixture.Mapper,
        _options,
        _podService,
        new GamespaceStore(_fixture.Db),
        _fixture.Store,
        null,
        new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
        _guard
    );

    [Fact]
    public async Task GamespaceStart_FitsWithoutAWarning()
    {
        await AddGamespace(("a", 2), ("b", 2));
        _options.CapacityCheck = CapacityCheckMode.Block;

        var state = await GamespaceService().Start(GamespaceId);

        Assert.Null(state.CapacityWarning);
        Assert.Equal(2, _pod.Vms.Count);
    }

    [Fact]
    public async Task GamespaceStart_WarnDeploysAndCarriesTheWarning()
    {
        await AddGamespace(("a", 8), ("b", 8));
        _options.CapacityCheck = CapacityCheckMode.Warn;

        var state = await GamespaceService().Start(GamespaceId);

        Assert.Contains("needs 16.0 GB", state.CapacityWarning);
        Assert.Equal(2, _pod.Vms.Count);
    }

    [Fact]
    public async Task GamespaceStart_BlockRefusesBeforeDeploying()
    {
        await AddGamespace(("a", 8), ("b", 8));
        _options.CapacityCheck = CapacityCheckMode.Block;

        var ex = await Assert.ThrowsAsync<CapacityExceeded>(() => GamespaceService().Start(GamespaceId));

        Assert.Contains("10.0 GB is free", ex.Message);
        Assert.Equal(0, _pod.Deploys);
    }

    [Fact]
    public async Task GamespaceStart_CountsOnlyTheVmsStillMissing()
    {
        // 16 GB in all, but the 8 GB "a" is already running, so 8 GB more fits in 10 GB
        await AddGamespace(("a", 8), ("b", 8));
        _pod.Vms.Add(new Vm { Id = "a", Name = $"a#{GamespaceId}" });
        _options.CapacityCheck = CapacityCheckMode.Block;

        var state = await GamespaceService().Start(GamespaceId);

        Assert.Null(state.CapacityWarning);
        Assert.Equal(2, _pod.Vms.Count);
    }

    [Fact]
    public async Task GamespaceStart_HoldsItsMemoryUntilANewerSnapshot()
    {
        await AddGamespace(("a", 8));
        _options.CapacityCheck = CapacityCheckMode.Block;

        await GamespaceService().Start(GamespaceId);

        Assert.Equal(8192, _guard.ReservedMb(DateTimeOffset.MinValue));
    }

    [Fact]
    public void GameStateLeavesOutAnEmptyWarning()
    {
        var web = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        Assert.DoesNotContain("capacityWarning", JsonSerializer.Serialize(new GameState { Name = "x" }, web));
        Assert.Contains("\"capacityWarning\":\"short\"", JsonSerializer.Serialize(new GameState { Name = "x", CapacityWarning = "short" }, web));
        Assert.DoesNotContain("capacityWarning", JsonSerializer.Serialize(new Vm(), web));
    }

    // ---- refusal on the wire ----

    [Fact]
    public async Task Refusal_Is409WithTheReason()
    {
        var middleware = new JsonExceptionMiddleware(
            _ => throw new CapacityExceeded("Not enough hypervisor memory: this deploy needs 16.0 GB and 10.0 GB is free after 20% headroom."),
            NullLogger<JsonExceptionMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        await middleware.Invoke(context, cache);

        context.Response.Body.Position = 0;
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();

        Assert.Equal(409, context.Response.StatusCode);
        Assert.Contains("needs 16.0 GB and 10.0 GB is free", body);
        // a refusal is a warning in the log, not an entry in the admin error list
        Assert.Null(cache.Get(AppConstants.ErrorListCacheKey));
    }

    /// <summary>
    /// VmController with the actor set the way OnActionExecuting would, and a hub that drops events.
    /// </summary>
    private sealed class TestVmController : VmController
    {
        public TestVmController(IHypervisorService pod, CoreOptions options, CapacityGuard guard, TemplateService templates, User actor)
            : base(NullLogger<AdminController>.Instance, NullHub.Create(), templates, null, pod, options, guard)
        {
            Actor = actor;
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        }
    }
}

/// <summary>
/// A SignalR hub context whose clients accept every event and do nothing.
/// </summary>
public class NullHub : DispatchProxy
{
    public static IHubContext<AppHub, IHubEvent> Create() => Create<IHubContext<AppHub, IHubEvent>, NullHub>();

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        Type type = targetMethod.ReturnType;

        if (type == typeof(Task))
            return Task.CompletedTask;

        if (type.IsInterface)
        {
            return typeof(DispatchProxy)
                .GetMethod(nameof(DispatchProxy.Create), 2, Type.EmptyTypes)
                .MakeGenericMethod(type, typeof(NullHub))
                .Invoke(null, null);
        }

        return null;
    }
}
