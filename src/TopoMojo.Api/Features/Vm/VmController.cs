// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Services;
using TopoMojo.Api.Validators;
using TopoMojo.Api.Models;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Controllers;

[Authorize]
[ApiController]
[TypeFilter<VmValidator>]
public class VmController(
    ILogger<AdminController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    TemplateService templateService,
    UserService userService,
    IHypervisorService podService,
    CoreOptions options
    ) : BaseController(logger, hub)
{

    /// <summary>
    /// List vms.
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    [HttpGet("api/vms")]
    [SwaggerOperation(OperationId = "ListVms")]
    [Authorize]

    public async Task<ActionResult<Vm[]>> ListVms([FromQuery] string filter)
    {
        if (!AuthorizeAny(
            () => Actor.IsObserver
        )) return Forbid();

        var vms = await podService.Find(filter);

        if (Actor.IsObserver && !Actor.IsAdmin)
        {
            // filter by scope/audience: ensure all VMs come from workspaces w/ audiences
            // within this user's scope or they are manager
            vms = vms.Where(vm =>
                CanManageVm(vm.Name, Actor).Result
            ).ToArray();
        }

        var keys = vms.Select(v => v.Name.Tag()).Distinct().ToArray();

        var map = await templateService.ResolveKeys(keys);

        foreach (Vm vm in vms)
            vm.GroupName = map[vm.Name.Tag()];

        return Ok(
            vms
            .OrderBy(v => v.GroupName)
            .ThenBy(v => v.Name)
            .ToArray()
        );
    }

    /// <summary>
    /// Load a vm.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("api/vm/{id}")]
    [SwaggerOperation(OperationId = "LoadVm")]
    [Authorize]
    public async Task<ActionResult<Vm>> LoadVm(string id)
    {
        if (!AuthorizeAny(
            () => CanManageVm(id, Actor).Result
        )) return Forbid();

        return Ok(
            await podService.Load(id)
        );
    }

    /// <summary>
    /// Change vm state.
    /// </summary>
    /// <remarks>
    /// Operations: Start, Stop, Save, Revert
    /// </remarks>
    /// <param name="op"></param>
    /// <returns></returns>
    [HttpPut("api/vm")]
    [SwaggerOperation(OperationId = "ChangeVm")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<Vm>> ChangeVm([FromBody] VmOperation op)
    {
        if (!AuthorizeAny(
            () => CanManageVmOperation(op).Result
        )) return Forbid();

        Vm vm = await podService.ChangeState(op);

        SendBroadcast(vm, op.Type.ToString().ToLower());

        return Ok(vm);
    }

    /// <summary>
    /// Delete a vm.
    /// </summary>
    /// <param name="id">Vm Id</param>
    /// <returns></returns>
    [HttpDelete("api/vm/{id}")]
    [SwaggerOperation(OperationId = "DeleteVm")]
    [Authorize]
    public async Task<ActionResult<Vm>> DeleteVm(string id)
    {
        if (!AuthorizeAny(
            () => CanDeleteVm(id, Actor.Id).Result
        )) return Forbid();

        Vm vm = await podService.Delete(id);

        SendBroadcast(vm, "delete");

        return Ok(vm);
    }

    /// <summary>
    /// Change vm iso or network
    /// </summary>
    /// <param name="id">Vm Id</param>
    /// <param name="change">key-value pairs</param>
    /// <returns></returns>
    [HttpPut("api/vm/{id}/change")]
    [SwaggerOperation(OperationId = "ReconfigureVm")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<Vm>> ReconfigureVm(string id, [FromBody] VmKeyValue change)
    {
        if (!AuthorizeAny(
            () => CanManageVm(id, Actor).Result
        )) return Forbid();

        // need elevated privileges to change vm to special nets
        if (
            Actor.IsBuilder.Equals(false) &&
            change.Key == "net" &&
            change.Value.Contains(AppConstants.TagDelimiter).Equals(false) &&
            options.AllowUnprivilegedVmReconfigure.Equals(false)
        )
        {
            throw new ActionForbidden();
        }

        bool sudo = Actor.IsBuilder && options.AllowPrivilegedNetworkIsolationExemption;

        Vm vm = await podService.ChangeConfiguration(id, change, sudo);

        SendBroadcast(vm, "change");

        return Ok(vm);
    }

    /// <summary>
    /// Answer a vm question.
    /// </summary>
    /// <param name="id">Vm Id</param>
    /// <param name="answer"></param>
    /// <returns></returns>
    [HttpPut("api/vm/{id}/answer")]
    [SwaggerOperation(OperationId = "AnswerVmQuestion")]
    [Authorize]
    public async Task<ActionResult<Vm>> AnswerVmQuestion(string id, [FromBody] VmAnswer answer)
    {
        if (!AuthorizeAny(
            () => CanManageVm(id, Actor).Result
        )) return Forbid();

        Vm vm = await podService.Answer(id, answer);

        SendBroadcast(vm, "answer");

        return Ok(vm);
    }

    /// <summary>
    /// Find ISO files available to a vm.
    /// </summary>
    /// <param name="id">Vm Id</param>
    /// <returns></returns>
    [HttpGet("api/vm/{id}/isos")]
    [SwaggerOperation(OperationId = "GetVmIsoOptions")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<VmOptions>> GetVmIsoOptions(string id)
    {
        if (!AuthorizeAny(
            () => CanManageVm(id, Actor).Result
        )) return Forbid();

        return Ok(
            await podService.GetVmIsoOptions(
                await GetVmIsolationTag(id)
            )
        );
    }

    /// <summary>
    /// Find virtual networks available to a vm.
    /// </summary>
    /// <param name="id">Vm Id</param>
    /// <returns></returns>
    [HttpGet("api/vm/{id}/nets")]
    [SwaggerOperation(OperationId = "GetVmNetOptions")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<VmOptions>> GetVmNetOptions(string id)
    {
        if (!AuthorizeAny(
            () => CanManageVm(id, Actor).Result
        )) return Forbid();

        var opt = await podService.GetVmNetOptions(
            await GetVmIsolationTag(id)
        );

        // if not builder, strip any privileged nets
        if (
            Actor.IsBuilder.Equals(false) &&
            options.AllowUnprivilegedVmReconfigure.Equals(false)
        )
        {
            opt.Net = opt.Net.Where(x => x.Contains(AppConstants.TagDelimiter)).ToArray();
        }

        return Ok(opt);
    }

    /// <summary>
    /// Request a vm console access ticket.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("api/vm-console/{id}")]
    [SwaggerOperation(OperationId = "GetVmTicket")]
    [Authorize(AppConstants.AnyUserPolicy)]
    public async Task<ActionResult<VmConsole>> GetVmTicket(string id)
    {
        if (!AuthorizeAny(
            () => CanManageVm(id, Actor).Result
        )) return Forbid();

        var info = await podService.Display(id);

        if (info.Url.IsEmpty())
            return Ok(info);

        Logger.LogDebug("mks url: {url}", info.Url);

        var src = new Uri(info.Url);
        string target = "";
        string qs = "";
        string internalHost = src.Host.Split('.').First();
        string domain = Request.Host.Value.Contains('.')
                    ? Request.Host.Value[(Request.Host.Value.IndexOf('.') + 1)..]
                    : Request.Host.Value;

        switch (podService.Options.TicketUrlHandler.ToLower())
        {
            case "local-app":
                target = $"{Request.Host.Value}{Request.PathBase}{internalHost}";
                break;

            case "external-domain":
                target = $"{internalHost}.{domain}";
                break;

            case "host-map":
                var map = podService.Options.TicketUrlHostMap;
                if (map.TryGetValue(src.Host, out string value))
                    target = value;
                break;

            // TODO: make this default after publishing change
            case "none":
            case "":
                break;

            case "querystring":
            default:
                qs = $"?vmhost={src.Host}";
                target = options.ConsoleHost;
                break;
        }

        if (target.NotEmpty())
            info.Url = info.Url.Replace(src.Host, target);

        info.Url += qs;

        Logger.LogDebug("mks url: {url}", info.Url);

        return Ok(info);
    }

    /// <summary>
    /// Resolve a vm from a template.
    /// </summary>
    /// <param name="id">Template Id</param>
    /// <returns></returns>
    [HttpGet("api/vm-template/{id}")]
    [SwaggerOperation(OperationId = "ResolveVmFromTemplate")]
    [Authorize]
    public async Task<ActionResult<Vm>> ResolveVmFromTemplate(string id)
    {
        var template = await templateService.GetDeployableTemplate(id, null);

        string name = $"{template.Name}{AppConstants.TagDelimiter}{template.IsolationTag}";

        if (!AuthorizeAny(
            () => CanManageVm(name, Actor).Result
        )) return Forbid();

        return Ok(
            await podService.Refresh(template)
        );
    }

    /// <summary>
    /// Deploy a vm from a template.
    /// </summary>
    /// <param name="id">Template Id</param>
    /// <returns></returns>
    [HttpPost("api/vm-template/{id}")]
    [SwaggerOperation(OperationId = "DeployVm")]
    [Authorize]
    public async Task<ActionResult<Vm>> DeployVm(string id)
    {
        VmTemplate template = await templateService
            .GetDeployableTemplate(id, null)
        ;

        string name = $"{template.Name}{AppConstants.TagDelimiter}{template.IsolationTag}";

        if (!AuthorizeAny(
            () => CanManageVm(name, Actor).Result
        )) return Forbid();

        Vm vm = await podService.Deploy(template, Actor.IsBuilder);

        if (template.HostAffinity)
        {
            await podService.SetAffinity(
                template.IsolationTag,
                [vm],
                true
            );

            vm.State = VmPowerState.Running;
        }

        // SendBroadcast(vm, "deploy");
        VmState state = new()
        {
            Id = template.Id.ToString(),
            Name = vm.Name.Untagged(),
            IsolationId = vm.Name.Tag(),
            IsRunning = vm.State == VmPowerState.Running
        };

        await Hub.Clients
            .Group(state.IsolationId)
            .VmEvent(new BroadcastEvent<VmState>(User, "VM.DEPLOY", state))
        ;

        return Ok(vm);
    }

    /// <summary>
    /// Initialize vm disks.
    /// </summary>
    /// <param name="id">Template Id</param>
    /// <returns></returns>
    [HttpPut("api/vm-template/{id}")]
    [SwaggerOperation(OperationId = "InitializeVmTemplate")]
    [Authorize]
    public async Task<ActionResult<int>> InitializeVmTemplate(string id)
    {
        VmTemplate template = await templateService.GetDeployableTemplate(id, null);

        string name = $"{template.Name}{AppConstants.TagDelimiter}{template.IsolationTag}";

        if (!AuthorizeAny(
            () => CanManageVm(name, Actor).Result
        )) return Forbid();

        return Ok(
            await podService.CreateDisks(template)
        );
    }

    /// <summary>
    /// Initiate hypervisor manager reload
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    [HttpPost("api/pod/{host}")]
    [Authorize(AppConstants.AdminOnlyPolicy)]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<ActionResult> ReloadHost(string host)
    {
        await podService.ReloadHost(host);
        return Ok();
    }

    private async Task<bool> CanDeleteVm(string id, string subjectId)
    {
        return await userService.CanInteract(
            subjectId,
            await GetVmIsolationTag(id)
        );
    }

    private async Task<bool> CanManageVm(string id, User actor)
    {
        string isolationTag = await GetVmIsolationTag(id);
        return actor.Id == isolationTag ||
            (actor.IsObserver && await userService.CanInteractWithAudience(actor.Scope, isolationTag)) ||
            (await userService.CanInteract(actor.Id, isolationTag));
    }

    private async Task<bool> CanManageVmOperation(VmOperation op)
    {
        return op.Type == VmOperationType.Delete
            ? await CanDeleteVm(op.Id, Actor.Id)
            : await CanManageVm(op.Id, Actor);
    }

    private async Task<string> GetVmIsolationTag(string id)
    {
        // id here can be name#isolationId, vm-id, or just isolationId
        return id.Contains(AppConstants.TagDelimiter)
            ? id.Tag()
            : (await podService.Load(id))?.Name.Tag() ?? id
        ;
    }

    private void SendBroadcast(Vm vm, string action)
    {
        VmState state = new()
        {
            Id = vm.Id,
            Name = vm.Name.Untagged(),
            IsolationId = vm.Name.Tag(),
            IsRunning = vm.State == VmPowerState.Running
        };

        Hub.Clients
            .Group(vm.Name.Tag())
            .VmEvent(new BroadcastEvent<VmState>(User, "VM." + action.ToUpper(), state))
        ;
    }
}
