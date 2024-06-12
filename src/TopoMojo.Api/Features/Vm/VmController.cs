// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Hubs;
using TopoMojo.Api.Services;
using TopoMojo.Api.Validators;
using TopoMojo.Api.Models;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Controllers
{
    [Authorize]
    [ApiController]
    public class VmController : _Controller
    {
        public VmController(
            ILogger<AdminController> logger,
            IHubContext<AppHub, IHubEvent> hub,
            VmValidator validator,
            TemplateService templateService,
            UserService userService,
            IHypervisorService podService,
            CoreOptions options
        ) : base(logger, hub, validator)
        {
            _templateService = templateService;
            _userService = userService;
            _pod = podService;
            _options = options;
        }

        private readonly IHypervisorService _pod;
        private readonly TemplateService _templateService;
        private readonly UserService _userService;
        private readonly CoreOptions _options;

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
            AuthorizeAny(
                () => Actor.IsObserver
            );

            var vms = await _pod.Find(filter);

            if (Actor.IsObserver && !Actor.IsAdmin)
            {
                // filter by scope/audience: ensure all VMs come from workspaces w/ audiences
                // within this user's scope or they are manager
                vms = vms.Where(vm =>
                    CanManageVm(vm.Name, Actor).Result
                ).ToArray();
            }

            var keys = vms.Select(v => v.Name.Tag()).Distinct().ToArray();

            var map = await _templateService.ResolveKeys(keys);

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
            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(id, Actor).Result
            );

            return Ok(
                await _pod.Load(id)
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
            await Validate(op);

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVmOperation(op).Result
            );

            Vm vm = await _pod.ChangeState(op);

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
            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanDeleteVm(id, Actor.Id).Result
            );

            Vm vm = await _pod.Delete(id);

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
            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(id, Actor).Result
            );

            // need elevated privileges to change vm to special nets
            if (
                Actor.IsBuilder.Equals(false) &&
                change.Key == "net" &&
                change.Value.Contains('#').Equals(false) &&
                _options.AllowUnprivilegedVmReconfigure.Equals(false)
            )
            {
                throw new ActionForbidden();
            }

            bool sudo = Actor.IsBuilder && _options.AllowPrivilegedNetworkIsolationExemption;

            Vm vm = await _pod.ChangeConfiguration(id, change, sudo);

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
            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(id, Actor).Result
            );

            Vm vm = await _pod.Answer(id, answer);

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
            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(id, Actor).Result
            );

            return Ok(
                await _pod.GetVmIsoOptions(
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
            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(id, Actor).Result
            );

            var opt = await _pod.GetVmNetOptions(
                await GetVmIsolationTag(id)
            );

            // if not builder, strip any privileged nets
            if (
                Actor.IsBuilder.Equals(false) &&
                _options.AllowUnprivilegedVmReconfigure.Equals(false)
            )
            {
                opt.Net = opt.Net.Where(x => x.Contains('#')).ToArray();
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
            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(id, Actor).Result
            );

            var info = await _pod.Display(id);

            if (info.Url.IsEmpty())
                return Ok(info);

            Logger.LogDebug($"mks url: {info.Url}");

            var src = new Uri(info.Url);
            string target = "";
            string qs = "";
            string internalHost = src.Host.Split('.').First();
            string domain = Request.Host.Value.Contains('.')
                        ? Request.Host.Value[(Request.Host.Value.IndexOf('.') + 1)..]
                        : Request.Host.Value;

            switch (_pod.Options.TicketUrlHandler.ToLower())
            {
                case "local-app":
                    target = $"{Request.Host.Value}{Request.PathBase}{internalHost}";
                    break;

                case "external-domain":
                    target = $"{internalHost}.{domain}";
                    break;

                case "host-map":
                    var map = _pod.Options.TicketUrlHostMap;
                    if (map.ContainsKey(src.Host))
                        target = map[src.Host];
                    break;

                // TODO: make this default after publishing change
                case "none":
                case "":
                    break;

                case "querystring":
                default:
                    qs = $"?vmhost={src.Host}";
                    target = _options.ConsoleHost;
                    break;
            }

            if (target.NotEmpty())
                info.Url = info.Url.Replace(src.Host, target);

            info.Url += qs;

            Logger.LogDebug($"mks url: {info.Url}");

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
            var template = await _templateService.GetDeployableTemplate(id, null);

            string name = $"{template.Name}#{template.IsolationTag}";

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(name, Actor).Result
            );

            return Ok(
                await _pod.Refresh(template)
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
            VmTemplate template = await _templateService
                .GetDeployableTemplate(id, null)
            ;

            string name = $"{template.Name}#{template.IsolationTag}";

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(name, Actor).Result
            );

            Vm vm = await _pod.Deploy(template, Actor.IsBuilder);

            if (template.HostAffinity)
            {
                await _pod.SetAffinity(
                    template.IsolationTag,
                    new Vm[] { vm },
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
            VmTemplate template = await _templateService.GetDeployableTemplate(id, null);

            string name = $"{template.Name}#{template.IsolationTag}";

            AuthorizeAny(
                () => Actor.IsAdmin,
                () => CanManageVm(name, Actor).Result
            );

            return Ok(
                await _pod.CreateDisks(template)
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
            await _pod.ReloadHost(host);
            return Ok();
        }

        private async Task<bool> CanDeleteVm(string id, string subjectId)
        {
            return await _userService.CanInteract(
                subjectId,
                await GetVmIsolationTag(id)
            );
        }

        private async Task<bool> CanManageVm(string id, User actor)
        {
            string isolationTag = await GetVmIsolationTag(id);
            return actor.Id == isolationTag ||
                (actor.IsObserver && await _userService.CanInteractWithAudience(actor.Scope, isolationTag)) ||
                (await _userService.CanInteract(actor.Id, isolationTag));
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
            return id.Contains("#")
                ? id.Tag()
                : (await _pod.Load(id))?.Name.Tag() ?? id
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
}
