// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor.Exceptions;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.Proxmox
{
    public partial class ProxmoxHypervisorService : IHypervisorService
    {
        public ProxmoxHypervisorService(
            HypervisorServiceConfiguration options,
            IProxmoxNameService nameService,
            IProxmoxVlanManager vlanManager,
            ILoggerFactory mill,
            Random random,
            IHttpClientFactory httpClientFactory
        )
        {
            _options = options;
            _mill = mill;
            _logger = _mill.CreateLogger<ProxmoxHypervisorService>();
            _vlanManager = vlanManager;
            _vmCache = new ConcurrentDictionary<string, Vm>();

            NormalizeOptions(_options);

            _pveClient = new ProxmoxClient(
                options,
                _vmCache,
                _mill.CreateLogger<ProxmoxClient>(),
                nameService,
                vlanManager,
                random,
                httpClientFactory);

            _ = Task.Run(DeploymentHandler);
        }

        private readonly HypervisorServiceConfiguration _options;

        private readonly ILogger<ProxmoxHypervisorService> _logger;
        private readonly ILoggerFactory _mill;
        private readonly ConcurrentDictionary<string, Vm> _vmCache;
        private readonly ProxmoxClient _pveClient;
        private readonly IProxmoxVlanManager _vlanManager;

        public HypervisorServiceConfiguration Options { get { return _options; } }
        private readonly BlockingCollection<DeploymentContext> DeploymentCollection = [];


        public async Task<Vm> Deploy(VmTemplate template, bool privileged = false)
        {
            var vm = await LoadVm($"{template.Name}#{template.IsolationTag}");
            if (vm != null)
                return vm;

            NormalizeTemplate(template, Options, privileged);
            _logger.LogDebug("deploy: {name} {host}", template.Name, Options.Host);
            return await _pveClient.Deploy(template);
        }

        public async Task<IEnumerable<Vm>> Deploy(IEnumerable<VmTemplate> templates, bool privileged = false)
        {
            var virtualNetworks = templates
                .SelectMany(t => t.Eth)
                .Select(eth => eth.Net)
                .ToArray();
            var vms = new List<Vm>();
            var undeployedTemplates = new List<VmTemplate>();

            foreach (var template in templates)
            {
                var vm = await LoadVm(template.Name + "#" + template.IsolationTag);
                if (vm is null)
                {
                    NormalizeTemplate(template, Options, privileged);
                    undeployedTemplates.Add(template);
                }

                _logger.LogDebug("deploy (host: {host}, templates: {count}): {templates}",
                    Options.Host,
                    undeployedTemplates.Count,
                    string.Join(",", undeployedTemplates.Select(t => t.Name))
                );
                _logger.LogDebug("deploy: {name} {host}", template.Name, Options.Host);

                vms.Add(await _pveClient.Deploy(template));
            }

            return vms;
        }

        public async Task<VmOptions> GetVmNetOptions(string id)
        {
            var hostVnets = await _vlanManager.GetVnets();

            return new VmOptions { Net = hostVnets.Select(n => n.Alias).ToArray() };
        }

        public string Version
        {
            get
            {
                return "TopoMojo Pod Manager for Proxmox, v1.0.0";
            }
        }

        private void NormalizeTemplate(VmTemplate template, HypervisorServiceConfiguration option, bool privileged = false)
        {
            foreach (VmDisk disk in template.Disks)
            {
                if (!disk.Path.StartsWith(option.DiskStore)
                )
                {
                    DatastorePath dspath = new(disk.Path);
                    dspath.Merge(option.DiskStore);
                    disk.Path = dspath.ToString();
                }

                if (disk.Source.HasValue() && !disk.Source.StartsWith(option.DiskStore)
                )
                {
                    DatastorePath dspath = new(disk.Source);
                    dspath.Merge(option.DiskStore);
                    disk.Source = dspath.ToString();
                }
            }

            if (template.IsolationTag.HasValue())
            {
                var tag = "#" + template.IsolationTag;
                var rgx = MyRegex();

                if (!template.Name.EndsWith(template.IsolationTag))
                    template.Name = rgx.Replace(template.Name, "") + tag;

                foreach (var requestedNetwork in template.Eth)
                {
                    if (privileged && _vlanManager.IsReserved(requestedNetwork.Net))
                        continue;

                    requestedNetwork.Net = rgx.Replace(requestedNetwork.Net, "") + tag;
                }
            }
        }

        public async Task<Vm> Delete(string id)
        {
            _logger.LogDebug("deleting {id}", id);
            Vm vm = await LoadVm(id);
            return await _pveClient.Delete(vm.Id);
        }

        public async Task<VmConsole> Display(string id)
        {
            var info = new VmConsole();

            try
            {
                var vm = await LoadVm(id);

                info = new VmConsole
                {
                    Id = vm.Id,
                    Name = vm.Name.Untagged(),
                    IsolationId = vm.Name.Tag(),
                    IsRunning = vm.State == VmPowerState.Running
                };

                // throws if powered off
                var ticket = await _pveClient.GetTicket(GetId(vm.Id));
                info.Url = ticket.Item1;
                info.Ticket = ticket.Item2;

            }
            catch { }

            return info;
        }

        private static string GetId(string id)
        {
            return id.Split('/').Last();
        }

        protected class HostVmCount
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        private static void NormalizeOptions(HypervisorServiceConfiguration options)
        {
            var regex = new Regex("(]|/)$");

            if (!regex.IsMatch(options.VmStore))
                options.VmStore += "/";

            if (!regex.IsMatch(options.DiskStore))
                options.DiskStore += "/";

            if (!regex.IsMatch(options.IsoStore))
                options.IsoStore += "/";
            ProxmoxIsoNaming.ValidateScopeSeparator(options.IsoScopeSeparator);
        }

        public async Task<Vm> Load(string id)
        {
            return await LoadVm(id, false);
        }

        private Task<Vm> LoadVm(string id, bool returnNull = true)
        {
            Vm vm = _vmCache.Values.Where(o => o.Id == id || o.Name == id).FirstOrDefault();

            if (vm == null && !returnNull)
            {
                vm = new Vm()
                {
                    Id = null,
                    HypervisorType = HypervisorType.Proxmox
                };
            }

            return Task.FromResult(vm);
        }

        private void CheckProgress(Vm vm)
        {
            if (vm.Task != null && (vm.Task.Progress < 0 || vm.Task.Progress > 99))
            {
                vm.Task = null;
                _vmCache.TryUpdate(vm.Id, vm, vm);
            }
        }

        private Vm[] CheckProgress(Vm[] vms)
        {
            foreach (Vm vm in vms)
                CheckProgress(vm);

            return vms;
        }

        public async Task<Vm> Start(string id)
        {
            var vm = await LoadVm(id);
            return await _pveClient.Start(vm.Id);
        }

        public async Task<Vm> Stop(string id)
        {
            var vm = await LoadVm(id);
            return await _pveClient.Stop(vm.Id);
        }

        private async Task<Vm> Reset(string id)
        {
            var vm = await LoadVm(id);
            return await _pveClient.Reset(vm.Id);
        }

        public async Task<Vm> Save(string id)
        {
            var vm = await LoadVm(id);
            return await _pveClient.Save(vm.Id);
        }

        public Task<Vm> Revert(string id)
        {
            throw new NotImplementedException();
        }

        public async Task StartAll(string target)
        {
            _logger.LogDebug("starting all matching {target}", target);
            var tasks = new List<Task>();
            foreach (var vm in await Find(target))
            {
                tasks.Add(Start(vm.Id));
            }

            await Task.WhenAll([.. tasks]);
        }

        public async Task StopAll(string target)
        {
            _logger.LogDebug("stopping all matching {target}", target);
            var tasks = new List<Task>();
            foreach (var vm in await Find(target))
            {
                tasks.Add(Stop(vm.Id));
            }
            await Task.WhenAll([.. tasks]);
        }

        public async Task DeleteAll(string target)
        {
            _logger.LogDebug("deleting all matching {target}", target);
            var tasks = new List<Task>();

            foreach (var vm in await Find(target))
            {
                tasks.Add(Delete(vm.Id));
            }

            await Task.WhenAll(tasks);
        }

        public async Task<Vm> ChangeState(VmOperation op)
        {
            Vm vm = null;
            var id = GetId(op.Id);
            switch (op.Type)
            {
                case VmOperationType.Start:
                    vm = await Start(op.Id);
                    break;

                case VmOperationType.Reset:
                    if (Options.EnableHA)
                    {
                        // a stop/start pair would be collapsed into a no-op by the HA manager
                        vm = await Reset(op.Id);
                    }
                    else
                    {
                        _ = await Stop(op.Id);
                        vm = await Start(op.Id);
                    }
                    break;

                case VmOperationType.Stop:
                    vm = await Stop(op.Id);
                    break;

                case VmOperationType.Save:
                    vm = await Save(id);
                    break;

                case VmOperationType.Revert:
                    vm = await Revert(op.Id);
                    break;

                case VmOperationType.Delete:
                    vm = await Delete(id);
                    break;
            }

            return vm;
        }

        public async Task<Vm> ChangeConfiguration(string id, VmKeyValue change, bool privileged = false)
        {
            if (!long.TryParse(id, out var vmId))
            {
                throw new ArgumentException($"Couldn't parse virtual machine ID to a long.", nameof(id));
            }

            var configUpdate = new PveVmUpdateConfig();

            switch (change.Key)
            {
                case "net":
                    // for NIC/network changes, the value contains the (topo) name of a virtual network.
                    // topo may also append a colon and the the zero-based index of the NIC to target, so
                    // we need to check if we're being asked to target a specific NIC. Defaults to the first
                    // NIC if not.
                    var nicIndex = 0;
                    var delimitedValue = change.Value.Split(':');
                    var netName = change.Value;

                    if (delimitedValue.Length == 2)
                    {
                        if (int.TryParse(delimitedValue[1], out nicIndex))
                        {
                            netName = delimitedValue[0];
                        }
                    }

                    configUpdate.NetAssignments[nicIndex] = netName.Trim();
                    break;
                default:
                    throw new NotImplementedException($"Updating configuration property '{change.Key}' is not supported on Proxmox.");
            }

            return await _pveClient.PushVmConfigUpdate(vmId, configUpdate);
        }

        public async Task SetAffinity(string isolationTag, Vm[] vms, bool start)
        {
            // affinity is expressed as an HA resource-affinity rule, so the vms have to be HA resources
            if (Options.EnableHA)
            {
                _logger.LogDebug("setaffinity: setting affinity for {tag}", isolationTag);
                await _pveClient.SetPositiveAffinity(isolationTag, vms);
            }
            else
            {
                _logger.LogWarning("setaffinity: host affinity on Proxmox requires Pod__EnableHA, ignoring for {tag}", isolationTag);
            }

            if (start)
                await Task.WhenAll(vms.Select(vm => Start(vm.Id)));
        }

        public async Task<Vm> Refresh(VmTemplate template)
        {
            string target = $"{template.Name}#{template.IsolationTag}";
            var vm = await LoadVm(target);

            if (vm == null)
            {
                if (_vmCache.Where(x => x.Value.Name == template.Template).Any())
                {
                    return new Vm
                    {
                        Name = target,
                        Status = "initialized"
                    };
                }
                else
                {
                    return new Vm
                    {
                        Name = target,
                        Status = "created"
                    };
                }
            }

            return vm;
        }

        public Task<Vm[]> Find(string term)
        {
            IEnumerable<Vm> q = _vmCache.Values.Where(x => !x.IsTemplate);

            if (term.HasValue())
                q = q.Where(o => o.Id.Contains(term) || o.Name.Contains(term));

            return Task.FromResult(q.ToArray());
        }

        public async Task<int> CreateDisks(VmTemplate template)
        {
            // Clone template
            var vm = await _pveClient.CreateTemplate(template);

            if (vm != null)
            {
                return 0;
            }
            else
            {
                return 100;
            }
        }

        public Task<int[]> VerifyDisks(VmTemplate template)
        {
            throw new NotImplementedException();
        }

        public async Task DeleteDisks(VmTemplate template)
        {
            await _pveClient.DeleteTemplate(template.Template);
        }

        public Task<Vm> Answer(string id, VmAnswer answer)
        {
            throw new NotImplementedException();
        }

        public async Task<VmOptions> GetVmIsoOptions(string key)
        {
            var isos = await this._pveClient.GetFiles();
            var publicKey = Guid.Empty.ToString();

            return new VmOptions
            {
                Iso = isos
                    .Where(x => x.ScopeId is not null)
                    .Where(x => string.Equals(key, publicKey, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.ScopeId, key, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(x.ScopeId, publicKey, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.DisplayName)
                    .ToArray()
            };
        }

        public Task<VmOptions> GetAllIsoOptions()
        {
            return GetVmIsoOptions(Guid.Empty.ToString());
        }

        public Task ReloadHost(string host)
        {
            throw new NotImplementedException();
        }

        private Task DeploymentHandler()
        {
            foreach (var ctx in DeploymentCollection.GetConsumingEnumerable())
                _ = DeployBatch(ctx);

            return Task.CompletedTask;
        }

        private async Task DeployBatch(DeploymentContext ctx)
        {
            var tasks = new List<Task<Vm>>();
            var existing = (await Find(ctx.Id)).Select(vm => vm.Name);
            var missing = ctx.Templates.Where(t => existing.Contains(t.Name).Equals(false));

            foreach (var template in missing)
                tasks.Add(Deploy(template, ctx.Privileged));

            await Task.WhenAll(tasks.ToArray());

            if (ctx.Affinity)
            {
                // templates with host affinity are deployed with AutoStart off, so SetAffinity
                // starts them after the rule is in place
                await SetAffinity(ctx.Id, [.. tasks.Select(t => t.Result).Where(vm => vm != null)], true);
            }
        }

        public async Task Deploy(DeploymentContext ctx, bool wait = false)
        {
            if (wait)
                await DeployBatch(ctx);
            else
                DeploymentCollection.Add(ctx);
        }

        // PVE storages are flat; ProxmoxIsoNaming encodes the scope into the filename.
        public string GetIsoStorePath(string scopeId, string fileName)
            => ProxmoxIsoNaming.Encode(scopeId, fileName, _options.IsoScopeSeparator);

        public IReadOnlyList<string> GetIsoStorePathCandidates(string scopeId, string fileName)
        {
            var current = GetIsoStorePath(scopeId, fileName);
            var legacy = ProxmoxIsoNaming.EncodeLegacy(scopeId, fileName);
            return string.Equals(current, legacy, StringComparison.Ordinal) ? [current] : [current, legacy];
        }

        public string GetIsoDatastorePath(string scopeId, string fileName)
            => ProxmoxIsoNaming.BuildDatastorePath(_options.IsoStore, scopeId, fileName);

        public async Task<string> UploadFileToDatastore(string datastorePath, string localFilePath)
        {
            var (storage, scopeId, fileName) = SplitIsoPath(datastorePath);
            _logger.LogInformation("Uploading Proxmox ISO {scopeId}/{fileName} to storage {storage}", scopeId, fileName, storage);

            try
            {
                // IHypervisorService exposes no ambient request token at this layer.
                await _pveClient.UploadIso(scopeId, fileName, localFilePath, CancellationToken.None);
                var volid = ProxmoxIsoNaming.BuildVolumeId(storage, ProxmoxIsoNaming.Encode(scopeId, fileName, _options.IsoScopeSeparator));

                _logger.LogInformation("Uploaded Proxmox ISO {volid}", volid);
                return volid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload Proxmox ISO {scopeId}/{fileName} to storage {storage}", scopeId, fileName, storage);
                throw;
            }
        }

        public async Task DeleteFileFromDatastore(string datastorePath)
        {
            var (storage, scopeId, fileName) = SplitIsoPath(datastorePath);
            _logger.LogInformation("Deleting Proxmox ISO {scopeId}/{fileName} from storage {storage}", scopeId, fileName, storage);

            try
            {
                await _pveClient.DeleteIso(scopeId, fileName);
                _logger.LogInformation("Deleted Proxmox ISO {scopeId}/{fileName} from storage {storage}", scopeId, fileName, storage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete Proxmox ISO {scopeId}/{fileName} from storage {storage}", scopeId, fileName, storage);
                throw;
            }
        }

        private (string storage, string scopeId, string fileName) SplitIsoPath(string datastorePath)
        {
            if (!ProxmoxIsoNaming.TrySplitDatastorePath(datastorePath, out var storage, out var scopeId, out var fileName)
                || !string.Equals(storage, ProxmoxIsoNaming.StorageName(Options.IsoStore), StringComparison.Ordinal))
            {
                throw new HypervisorException($"Unsupported Proxmox ISO path: {datastorePath}");
            }

            return (storage, scopeId, fileName);
        }

        [GeneratedRegex("#.*")]
        private static partial Regex MyRegex();
    }
}
