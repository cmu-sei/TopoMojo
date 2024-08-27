// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.Proxmox
{
    public class ProxmoxHypervisorService : IHypervisorService
    {
        public ProxmoxHypervisorService(
            HypervisorServiceConfiguration options,
            IProxmoxNameService nameService,
            IProxmoxVlanManager vlanManager,
            ILoggerFactory mill,
            Random random
        )
        {
            _options = options;
            _mill = mill;
            _logger = _mill.CreateLogger<ProxmoxHypervisorService>();
            // _hostCache = new ConcurrentDictionary<string, VimClient>();
            // _affinityMap = new Dictionary<string, VimClient>();
            _vlanManager = vlanManager;
            _vmCache = new ConcurrentDictionary<string, Vm>();

            NormalizeOptions(_options);

            _pveClient = new ProxmoxClient(
                options,
                _vmCache,
                _mill.CreateLogger<ProxmoxClient>(),
                nameService,
                vlanManager,
                random);
        }

        private readonly HypervisorServiceConfiguration _options;

        private readonly ILogger<ProxmoxHypervisorService> _logger;
        private readonly ILoggerFactory _mill;
        //private ConcurrentDictionary<string, VimClient> _hostCache;
        private DateTimeOffset _lastCacheUpdate = DateTimeOffset.MinValue;
        //private Dictionary<string, VimClient> _affinityMap;
        private readonly ConcurrentDictionary<string, Vm> _vmCache;
        private readonly ProxmoxClient _pveClient;
        private readonly IProxmoxVlanManager _vlanManager;

        public HypervisorServiceConfiguration Options { get { return _options; } }

        public async Task<Vm> Deploy(VmTemplate template, bool privileged = false)
        {
            var vm = await LoadVm(template.Name + "#" + template.IsolationTag);
            if (vm != null)
                return vm;

            _logger.LogDebug("deploy: host " + _options.Host);
            NormalizeTemplate(template, Options, privileged);
            _logger.LogDebug("deploy: normalized " + template.Name);

            _logger.LogDebug("deploy: " + template.Name + " " + Options.Host);
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
                    _logger.LogDebug("deploy: host " + _options.Host);
                    NormalizeTemplate(template, Options, privileged);
                    _logger.LogDebug("deploy: normalized " + template.Name);

                    undeployedTemplates.Add(template);
                }

                _logger.LogDebug($"deploy (host: {Options.Host}, templates: {undeployedTemplates.Count}): {string.Join(",", undeployedTemplates.Select(t => t.Name).ToArray())}");
                _logger.LogDebug("deploy: " + template.Name + " " + Options.Host);
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
            if (!template.Iso.HasValue())
            {
                // need to have a backing file to add the cdrom device
                template.Iso = option.IsoStore + "null.iso";
            }

            var isoPath = template.Iso.Replace('/', '#');
            isoPath = $"{option.IsoStore.Replace("/", String.Empty)}:iso/{isoPath}";
            template.Iso = isoPath;

            foreach (VmDisk disk in template.Disks)
            {
                if (!disk.Path.StartsWith(option.DiskStore)
                )
                {
                    DatastorePath dspath = new DatastorePath(disk.Path);
                    dspath.Merge(option.DiskStore);
                    disk.Path = dspath.ToString();
                }

                if (disk.Source.HasValue() && !disk.Source.StartsWith(option.DiskStore)
                )
                {
                    DatastorePath dspath = new DatastorePath(disk.Source);
                    dspath.Merge(option.DiskStore);
                    disk.Source = dspath.ToString();
                }
            }

            if (template.IsolationTag.HasValue())
            {
                var tag = "#" + template.IsolationTag;
                var rgx = new Regex("#.*");

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
            _logger.LogDebug("deleting " + id);
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

        private string GetId(string id)
        {
            var pveId = id.Split('/').Last();
            return pveId;
        }

        protected class HostVmCount
        {
            public string Name { get; set; }
            public int Count { get; set; }
        }

        private void NormalizeOptions(HypervisorServiceConfiguration options)
        {
            var regex = new Regex("(]|/)$");

            if (!regex.IsMatch(options.VmStore))
                options.VmStore += "/";

            if (!regex.IsMatch(options.DiskStore))
                options.DiskStore += "/";

            if (!regex.IsMatch(options.IsoStore))
                options.IsoStore += "/";
        }

        public async Task<Vm> Load(string id)
        {
            return await LoadVm(id, false);
        }

        private Task<Vm> LoadVm(string id, bool returnNull = true)
        {
            Vm vm = _vmCache.Values.Where(o => o.Id == id || o.Name == id).FirstOrDefault();

            if (vm != null)
            {
                //CheckProgress(vm);
            }
            else if (!returnNull)
            {
                vm = new Vm()
                {
                    Id = null
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
            _logger.LogDebug("starting all matching " + target);
            var tasks = new List<Task>();
            foreach (var vm in await Find(target))
            {
                tasks.Add(Start(vm.Id));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }

        public async Task StopAll(string target)
        {
            _logger.LogDebug("stopping all matching " + target);
            var tasks = new List<Task>();
            foreach (var vm in await Find(target))
            {
                tasks.Add(Stop(vm.Id));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }

        public async Task DeleteAll(string target)
        {
            _logger.LogDebug("deleting all matching " + target);
            await _pveClient.DeleteAll(target);
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
                    vm = await Stop(op.Id);
                    vm = await Start(op.Id);
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

        public Task<Vm> ChangeConfiguration(string id, VmKeyValue change, bool privileged = false)
        {
            throw new NotImplementedException();
        }

        public Task SetAffinity(string isolationTag, Vm[] vms, bool start)
        {
            throw new NotImplementedException();
        }

        public async Task<Vm> Refresh(VmTemplate template)
        {
            string target = template.Name + "#" + template.IsolationTag;
            Vm vm = await LoadVm(target);

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
            IEnumerable<Vm> q = _vmCache.Values;

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

            return new VmOptions
            {
                Iso = isos
                    .Where(x => x.Name.StartsWith(key) || x.Name.StartsWith(Guid.Empty.ToString()))
                    .Select(x => x.DisplayName)
                    .ToArray()
            };
        }

        public Task ReloadHost(string host)
        {
            throw new NotImplementedException();
        }
    }
}
