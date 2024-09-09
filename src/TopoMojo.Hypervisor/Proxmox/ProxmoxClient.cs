// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Corsinvest.ProxmoxVE.Api;
using Corsinvest.ProxmoxVE.Api.Extension;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Vm;
using TopoMojo.Hypervisor.Proxmox.Models;
using TopoMojo.Hypervisor.Extensions;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Node;
using System.Text;

namespace TopoMojo.Hypervisor.Proxmox
{
    public class ProxmoxClient
    {
        public ProxmoxClient(
            HypervisorServiceConfiguration options,
            ConcurrentDictionary<string, Vm> vmCache,
            ILogger<ProxmoxClient> logger,
            IProxmoxNameService nameService,
            IProxmoxVlanManager vnetService,
            Random random
        )
        {
            _logger = logger;
            _config = options;
            _logger.LogDebug($"Constructing Client {_config.Host}");
            _tasks = new Dictionary<string, PveNodeTask>();
            _vmCache = vmCache;
            _hostPrefix = _config.Host.Split('.').FirstOrDefault();
            _random = random;

            if (_config.Tenant == null)
                _config.Tenant = "";

            _pveClient = new PveClient(options.Host, 443)
            {
                ApiToken = options.AccessToken
            };

            _rootPveClient = new PveClient(options.Host, 443);

            _nameService = nameService;
            _vlanManager = vnetService;
            Task sessionMonitorTask = MonitorSession();
            Task taskMonitorTask = MonitorTasks();
        }

        private readonly ILogger<ProxmoxClient> _logger;
        Dictionary<string, PveNodeTask> _tasks;
        private ConcurrentDictionary<string, Vm> _vmCache;
        private readonly IProxmoxNameService _nameService;
        private readonly IProxmoxVlanManager _vlanManager;
        private readonly HypervisorServiceConfiguration _config = null;
        // int _pollInterval = 1000;
        int _syncInterval = 30000;
        int _taskMonitorInterval = 3000;
        string _hostPrefix = "";
        // DateTimeOffset _lastAction;
        private PveClient _pveClient;
        private PveClient _rootPveClient;
        private readonly Random _random;
        private readonly bool _enableHA = false;
        private readonly Object _lock = new object();
        private const string deleteTag = "delete"; // tags are always lower-case

        public async Task<Vm> Refresh(VmTemplate template)
        {
            string target = template.Name + "#" + template.IsolationTag;
            var resources = await _pveClient.GetResourcesAsync(ClusterResourceType.Vm);

            var pveVm = resources.Where(x => x.Name == _nameService.ToPveName(template.Name)).FirstOrDefault();

            if (pveVm != null)
            {
                return new Vm
                {
                    Name = _nameService.FromPveName(pveVm.Name),
                    Id = pveVm.VmId.ToString(),
                    State = pveVm.IsRunning ? VmPowerState.Running : VmPowerState.Off
                };
            }
            else
            {
                // todo: check for in progress cloning of parent template
                if (resources.Where(x => x.Name == template.Template).Any())
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
        }

        public async Task<Vm> CreateTemplate(VmTemplate template)
        {
            // await this.ReloadVmCache();

            var vmTemplate = _vmCache
                .Where(x => x.Value.Name == template.Template)
                .FirstOrDefault()
                .Value;

            if (vmTemplate != null)
            {
                throw new InvalidOperationException("Template already exists");
            }

            var parentTemplate = _vmCache
                .Where(x => x.Value.Name == template.ParentTemplate)
                .FirstOrDefault()
                .Value;

            if (parentTemplate == null)
            {
                throw new InvalidOperationException("Parent Template does not exist");
            }

            var nextId = await GetNextId();
            var pveId = Int32.Parse(nextId);
            var name = _nameService.ToPveName(template.Template);

            // full clone parent template
            var task = await _pveClient.Nodes[parentTemplate.Host].Qemu[parentTemplate.Id].Clone.CloneVm(
                pveId,
                full: true,
                name: name,
                target: parentTemplate.Host);
            await _pveClient.WaitForTaskToFinish(task);

            if (task.IsSuccessStatusCode)
            {
                // convert new vm to template
                task = await _pveClient.Nodes[parentTemplate.Host].Qemu[nextId].Template.Template();
                await _pveClient.WaitForTaskToFinish(task);
            }
            else
            {
                throw new Exception(task.ReasonPhrase);
            }

            Vm vm = new Vm()
            {
                Name = name,
                Id = nextId,
                State = VmPowerState.Off,
                Status = "deployed",
                Host = parentTemplate.Host,
                HypervisorType = HypervisorType.Proxmox
            };

            _vmCache.AddOrUpdate(vm.Id, vm, (k, v) => (v = vm));

            return vm;
        }

        public async Task<Vm> Deploy(VmTemplate template)
        {
            Result task;
            Vm vm = null;

            _logger.LogDebug("deploy: create vm...");
            var targetNode = await GetTargetNode();
            var vmTemplate = _vmCache
                .Where(x => x.Value.Name == template.Template &&
                            (x.Value.Tags == null || !x.Value.Tags.Contains(deleteTag)))
                .FirstOrDefault()
                .Value;

            var nextId = await GetNextId();
            var pveId = Int32.Parse(nextId);

            var cloneTask = _pveClient.Nodes[vmTemplate.Host].Qemu[vmTemplate.Id].Clone.CloneVm(
                pveId,
                full: false,
                name: _nameService.ToPveName(template.Name),
                target: targetNode);

            _logger.LogDebug($"deploy: virtual networks (id {template.Id})...");
            var vnetsTask = _vlanManager.Provision(template.Eth.Select(n => n.Net));
            var isoTask = this.GetIso(template);

            // We can clone vm and provision networks concurrently since we don't set the network until
            // the configure step after the clone is finished. Isos are also not dependent on the other tasks.
            await Task.WhenAll(cloneTask, vnetsTask, isoTask);

            task = cloneTask.Result;
            _logger.LogDebug($"deploy: {vnetsTask.Result.Count()} networks deployed.");

            await _pveClient.WaitForTaskToFinish(task);

            if (task.IsSuccessStatusCode)
            {
                // Proxmox requires using the root user account directly (not an access token)
                // for setting the args field to add arbitrary arguments to the QEMU command line. This is currently the only
                // way to set the fw_cfg property that we need for Guest Settings.
                // A Patch is available to add direct fw_cfg support but has not been merged into a release.
                // https://bugzilla.proxmox.com/show_bug.cgi?id=4068
                // If no root password is provided, skip setting args.
                var client = _pveClient;
                var setGuestSettings = false;

                if (!string.IsNullOrEmpty(_config.Password))
                {
                    if (await _rootPveClient.LoginAsync("root", _config.Password))
                    {
                        client = _rootPveClient;
                        setGuestSettings = true;
                    }
                    else
                    {
                        _logger.LogError("Error logging in with root password. Skipping Guest Settings.");
                    }
                }
                else
                {
                    _logger.LogDebug("No root password provided. Skipping Guest Settings");
                }

                var nics = await this.GetNics(template);
                var memory = this.GetMemory(template);
                var sockets = this.GetSockets(template);
                var coresPerSocket = this.GetCoresPerSocket(template);
                string args = null;

                if (setGuestSettings)
                {
                    args = this.GetArgs(template);
                }

                task = await client.Nodes[targetNode].Qemu[nextId].Config.UpdateVmAsync(
                    netN: nics,
                    memory: memory,
                    sockets: sockets,
                    cores: coresPerSocket,
                    cdrom: isoTask.Result,
                    args: args);
                await _pveClient.WaitForTaskToFinish(task);

                if (!task.IsSuccessStatusCode)
                {
                    _logger.LogError(task.ReasonPhrase);
                }

                _logger.LogDebug("deploy: load vm...");

                vm = new Vm()
                {
                    Name = template.Name,
                    Id = nextId,
                    State = VmPowerState.Off,
                    Status = "deployed",
                    Host = targetNode,
                    HypervisorType = HypervisorType.Proxmox
                };

                if (vm.Name.Contains("#").Equals(false) || vm.Name.ToTenant() != _config.Tenant)
                    return null;

                _vmCache.AddOrUpdate(vm.Id, vm, (k, v) => v = vm);

                if (_enableHA)
                {
                    task = await _pveClient.Cluster.Ha.Resources.Create(nextId);
                }

                if (template.AutoStart && task.IsSuccessStatusCode)
                {
                    _logger.LogDebug("deploy: start vm...");
                    vm = await Start(vm.Id);
                }
            }
            else
            {
                throw new Exception(task.ReasonPhrase);
            }

            return vm;
        }

        public async Task<Vm> Start(string id)
        {
            Vm vm = _vmCache[id];

            var task = await _pveClient.Nodes[vm.Host].Qemu[vm.GetId()].Status.Start.VmStart();
            await _pveClient.WaitForTaskToFinish(task);

            if (task.IsSuccessStatusCode)
            {
                vm.State = VmPowerState.Running;
            }
            else
            {
                throw new Exception(task.ReasonPhrase);
            }

            _vmCache.TryUpdate(vm.Id, vm, vm);

            return vm;
        }

        public async Task<Vm> Stop(string id)
        {
            Vm vm = _vmCache[id];

            var task = await _pveClient.Nodes[vm.Host].Qemu[vm.GetId()].Status.Stop.VmStop();
            await _pveClient.WaitForTaskToFinish(task);

            if (task.IsSuccessStatusCode)
            {
                vm.State = VmPowerState.Off;
            }
            else
            {
                throw new Exception(task.ReasonPhrase);
            }

            _vmCache.TryUpdate(vm.Id, vm, vm);

            return vm;
        }

        public async Task<Vm> DeleteTemplate(string templateName)
        {
            Vm vm = _vmCache.Where(x => x.Value.Name == templateName).FirstOrDefault().Value;

            if (vm == null)
                return null;

            return await Delete(vm.Id);
        }

        public async Task<Vm> Delete(string id)
        {
            Result task;
            var pveId = long.Parse(id);
            Vm vm = _vmCache[id];
            var status = await _pveClient.Nodes[vm.Host].Qemu[pveId].Status.Current.GetAsync();

            if (_enableHA)
            {
                task = await _pveClient.Cluster.Ha.Resources[pveId].Delete();
                await _pveClient.WaitForTaskToFinish(task);
            }

            if (status.IsRunning)
            {
                task = await _pveClient.ChangeStatusVmAsync(pveId, VmStatus.Stop);
                await _pveClient.WaitForTaskToFinish(task);
            }

            task = await _pveClient.Nodes[vm.Host].Qemu[id].DestroyVm();
            await _pveClient.WaitForTaskToFinish(task);

            if (task.IsSuccessStatusCode)
            {
                vm.Status = "initialized";
            }

            // Don't set vm to the result here, because if we get unlucky and the sync task removed
            // this vm from the cache first, we'll get a null value, which will cause errors in the
            // calling method
            _vmCache.TryRemove(vm.Id, out _);

            return vm;
        }

        public async Task<Tuple<string, string>> GetTicket(string id)
        {
            string url;
            string ticket;
            var vm = _vmCache[id];

            var result = await _pveClient.Nodes[vm.Host].Qemu[id].Vncproxy.Vncproxy(websocket: true);

            if (result.IsSuccessStatusCode)
            {
                string urlFragment = $"/api2/json/nodes/{vm.Host}/qemu/{id}/vncwebsocket?port={result.Response.data.port}&vncticket={WebUtility.UrlEncode(result.Response.data.ticket)}";
                url = $"wss://{_config.Host}{urlFragment}";
                ticket = result.Response.data.ticket;
            }
            else
            {
                throw new Exception(result.GetError());
            }

            return new Tuple<string, string>(url, ticket);
        }

        public async Task<Vm> Save(string id)
        {
            Vm vm = _vmCache[id];

            if (vm != null)
            {
                var config = await _pveClient.Nodes[vm.Host].Qemu[vm.Id].Config.GetAsync();

                var disk = config.Disks.ElementAt(0);
                var storageItems = await _pveClient.Nodes[vm.Host].Storage[disk.Storage].Content.GetAsync();

                var pveDisk = storageItems.Where(x => disk.FileName == x.FileName).FirstOrDefault();

                if (pveDisk != null)
                {
                    var parent = storageItems.Where(x => x.FileName == pveDisk.GetParentFilename()).FirstOrDefault();

                    if (parent != null && parent.Parent == null)
                    {
                        // check if anything else is using the template
                        var count = storageItems.Where(x => x.Parent != null && x.GetParentFilename() == parent.FileName).Count();

                        if (count > 1)
                        {
                            throw new InvalidOperationException("Base Template is in use");
                        }

                        var template = _vmCache[parent.VmId.ToString()];

                        if (template != null)
                        {
                            // Full Clone vm
                            var nextId = Int32.Parse(await this.GetNextId());
                            var task = await _pveClient.Nodes[vm.Host].Qemu[vm.Id].Clone.CloneVm(newid: nextId, full: true, target: template.Host, name: template.Name);

                            var t = new PveNodeTask { Id = task.Response.data, Action = "saving", WhenCreated = DateTimeOffset.UtcNow };
                            vm.Task = new VmTask { Name = "saving", WhenCreated = DateTime.UtcNow, Progress = t.Progress };
                            _tasks.Add(vm.Id, t);

                            _ = CompleteSave(task, id, nextId, template, vm.Id);
                        }
                    }
                }
            }

            return vm;
        }

        private async Task CompleteSave(Result task, string oldId, int nextId, Vm template, string vmId)
        {
            try
            {
                await _pveClient.WaitForTaskToFinish(task);

                if (!task.IsSuccessStatusCode)
                    throw new Exception($"Clone failed: {task.ReasonPhrase}");

                // Convert to template
                task = await _pveClient.Nodes[template.Host].Qemu[nextId].Template.Template();
                await _pveClient.WaitForTaskToFinish(task);

                if (!task.IsSuccessStatusCode)
                    throw new Exception($"Convert to template failed: {task.ReasonPhrase}");

                // Delete old vm
                await this.Delete(oldId);

                // Tag old template
                // Janitor will delete anything with this tag if deletion fails now
                task = await _pveClient
                    .Nodes[template.Host]
                    .Qemu[template.Id]
                    .Config
                    .UpdateVmAsync(tags: deleteTag);
                await _pveClient.WaitForTaskToFinish(task);

                if (!task.IsSuccessStatusCode)
                    throw new Exception($"Rename old template failed: {task.ReasonPhrase}");

                // delete old template
                task = await _pveClient.Nodes[template.Host].Qemu[template.Id].DestroyVm();
                await _pveClient.WaitForTaskToFinish(task);

                if (!task.IsSuccessStatusCode)
                    throw new Exception($"Delete old template failed: {task.ReasonPhrase}");

                await this.ReloadVmCache();
            }
            finally
            {
                _tasks.Remove(vmId);
            }
        }

        public async Task<PveIso[]> GetFiles()
        {
            var node = await this.GetRandomNode();

            var task = await _pveClient
                .Nodes[node]
                .Storage[_config.IsoStore]
                .Content
                .Index(content: "iso");
            await _pveClient.WaitForTaskToFinish(task);

            var isos = task.ToModel<PveIso[]>();

            return isos;
        }

        public Task<PveVmConfig> GetVmConfig(Vm vm)
            => GetVmConfig(vm.Host, vm.GetId());

        public async Task<PveVmConfig> GetVmConfig(string node, long vmId)
        {
            var vmConfig = await _pveClient
                .Nodes[node]
                .Qemu[vmId]
                .Config
                .GetAsync(true);

            var nics = new List<PveNic>();

            // our proxmox package stuffs NIC info into the ExtensionData property of
            // the config call, so we rely on the fact that (current) proxmox documentation
            // says that NICs start with "net" and are followed by a number
            var nicDataRegex = new Regex(@"net(\d)+");
            var nicModelMacRegex = new Regex(@"(?<model>[^=]+)=(?<mac>[0-9A-Fa-f:]{17})");

            if (vmConfig.ExtensionData.Any(d => nicDataRegex.IsMatch(d.Key)))
            {
                foreach (var extensionItem in vmConfig.ExtensionData)
                {
                    var match = nicDataRegex.Match(extensionItem.Key);

                    if (match.Success && int.TryParse(match.Groups[1].Value, out var nicIndex))
                    {
                        var modelMacMatch = nicModelMacRegex.Match(extensionItem.Value.ToString());

                        if (modelMacMatch.Success)
                        {
                            nics.Add(new PveNic
                            {
                                Index = nicIndex,
                                MacAddress = modelMacMatch.Groups["mac"].Value,
                                PveModel = modelMacMatch.Groups["model"].Value
                            });
                        }
                    }
                }
            }

            return new PveVmConfig
            {
                Boot = vmConfig.Boot,
                Cores = vmConfig.Cores,
                Cpu = vmConfig.Cpu,
                Nics = nics,
                OsType = vmConfig.OsType,
                MemoryInBytes = vmConfig.Memory,
                Smbios1 = vmConfig.Smbios1,
                Sockets = vmConfig.Sockets
            };
        }

        public async Task<Vm> PushVmConfigUpdate(long vmId, PveVmUpdateConfig update)
        {
            var vm = _vmCache[vmId.ToString()];
            var currentConfig = await this.GetVmConfig(vm);

            // if there are any net assignment updates, we need to resolve their IDs from the names
            // passed in. We make a new dictionary to hold them rather than mutate the argument.
            var vnetAssignmentIds = new Dictionary<int, string>();

            if (update.NetAssignments.Any())
            {
                var vnets = await _vlanManager.GetVnets();

                foreach (var netUpdate in update.NetAssignments)
                {
                    var resolvedName = _vlanManager.ResolvePveNetName(netUpdate.Value);
                    var vnet = vnets.FirstOrDefault(v => v.Alias == _vlanManager.ResolvePveNetName(netUpdate.Value))
                        ?? throw new Exception($"Couldn't resolve an ID for virtual network {netUpdate.Value}");

                    var nic = currentConfig.Nics.SingleOrDefault(n => n.Index == netUpdate.Key)
                        ?? throw new Exception($"Couldn't resolve a NIC on the host machine with index {netUpdate.Key}.");

                    var updateValue = $"{nic.PveModel}={nic.MacAddress},bridge={vnet.Vnet}";
                    vnetAssignmentIds.Add(netUpdate.Key, updateValue);
                }
            }

            var updateTask = await _pveClient
                .Nodes[vm.Host]
                .Qemu[vm.Id]
                .Config
                .UpdateVmAsync
                (
                    netN: vnetAssignmentIds.Any() ? vnetAssignmentIds : null
                );

            await _pveClient.WaitForTaskToFinish(updateTask);

            if (!updateTask.IsSuccessStatusCode)
            {
                throw new Exception($"VM Id {vmId}: failed to push update to the VM. The API returned a failed status code ({updateTask.StatusCode}): {updateTask.ReasonPhrase}");
            }

            return vm;
        }

        private async Task<string> GetTargetNode()
        {
            string target = null;
            var nodes = await _pveClient.GetNodesAsync();

            if (nodes.Count() > 0)
            {
                var targetNode = nodes
                    .OrderBy(x => x.MemoryUsagePercentage)
                    .Where(x => x.IsOnline)
                    .FirstOrDefault();

                target = targetNode.Node;
            }

            return target;
        }

        private async Task<string> GetRandomNode()
        {
            var nodes = await _pveClient.GetNodesAsync();
            var randomNum = _random.Next(0, nodes.Count() - 1);
            return nodes.ElementAt(randomNum).Node;
        }

        private string GetArgs(VmTemplate template)
        {
            if (!template.GuestSettings.Any())
                return null;

            var args = new StringBuilder();

            // Default settings
            // TODO: Fix duplication with vsphere?
            args.Append($"-fw_cfg name=opt/guestinfo.isolationTag,string={template.IsolationTag} ");
            args.Append($"-fw_cfg name=opt/guestinfo.templateSource,string={template.Id} ");
            args.Append($"-fw_cfg name=opt/guestinfo.hostname,string={template.Name.Untagged()} ");

            foreach (var setting in template.GuestSettings)
            {
                // TODO: rework this quick fix for injecting isolation specific settings
                if (setting.Key.StartsWith("iftag.") && !setting.Value.Contains(template.IsolationTag))
                {
                    continue;
                }

                args.Append($"-fw_cfg name=opt/{setting.Key},string={setting.Value} ");
            }

            return args.ToString().TrimEnd();
        }

        private async Task<string> GetIso(VmTemplate template)
        {
            var isos = await this.GetFiles();

            var iso = isos
                .Where(x => x.Volid == template.Iso)
                .FirstOrDefault();

            if (iso != null)
            {
                return iso.Volid;
            }
            else
            {
                return null;
            }
        }

        private string GetMemory(VmTemplate template)
        {
            return ((template.Ram > 0) ? template.Ram * 1024 : 1024).ToString();
        }

        private int? GetCoresPerSocket(VmTemplate template)
        {
            string[] p = template.Cpu.Split('x');
            int coresPerSocket = 1;

            if (p.Length > 1)
            {
                if (!Int32.TryParse(p[1], out coresPerSocket))
                {
                    coresPerSocket = 1;
                }
            }

            return coresPerSocket;
        }

        private int? GetSockets(VmTemplate template)
        {
            string[] p = template.Cpu.Split('x');
            var sockets = 1;

            if (!Int32.TryParse(p[0], out sockets))
            {
                sockets = 1;
            }

            return sockets;
        }

        private async Task<string> GetNextId()
        {
            string nextId = null;

            for (int i = 0; i < 10; i++)
            {
                var randomId = _random.Next(1, 999999999);
                var task = await _pveClient.Cluster.Nextid.Nextid(randomId);

                if (task.IsSuccessStatusCode)
                {
                    nextId = task.Response.data;
                    break;
                }
            }

            return nextId;
        }

        private async Task<Dictionary<int, string>> GetNics(VmTemplate template)
        {
            Dictionary<int, string> nics = new Dictionary<int, string>();

            if (template.Eth.IsEmpty())
                return nics;

            Result task;

            task = await _pveClient.Cluster.Sdn.Vnets.Index();
            await _pveClient.WaitForTaskToFinish(task);
            var vnets = task.ToModel<PveVnet[]>();

            for (int i = 0; i < template.Eth.Length; i++)
            {
                var eth = template.Eth[i];

                var vnet = vnets.Where(x => x.Alias == _nameService.ToPveName(eth.Net)).FirstOrDefault();
                if (vnet != null)
                {
                    var netString = new StringBuilder();
                    netString.Append(eth.Type);

                    if (!string.IsNullOrEmpty(eth.Mac))
                    {
                        netString.Append($"={eth.Mac.ToUpper()}");
                    }

                    nics.Add(i, $"{netString},bridge={vnet.Vnet}");
                }
            }

            return nics;
        }

        private Vm LoadVm(IClusterResourceVm pveVm)
        {
            Vm vm = new Vm()
            {
                Name = pveVm.Name == null ? "" : _nameService.FromPveName(pveVm.Name),
                Id = pveVm.VmId.ToString(),
                State = pveVm.IsRunning ? VmPowerState.Running : VmPowerState.Off,
                Status = "deployed",
                Host = pveVm.Node,
                Tags = pveVm.Tags == null ? new string[] { } : pveVm.Tags.Split(' '),
                HypervisorType = HypervisorType.Proxmox
            };

            if (_tasks.ContainsKey(vm.Id))
            {
                var t = _tasks[vm.Id];
                vm.Task = new VmTask { Name = t.Action, WhenCreated = t.WhenCreated, Progress = t.Progress };
            }

            // Proxmox Vm names are null for a few seconds when first deployed.
            // We still want to add to cache when in this state.
            if (!pveVm.IsTemplate && !string.IsNullOrEmpty(vm.Name) && vm.Name.Contains("#").Equals(false) ||
                vm.Name.ToTenant() != _config.Tenant)
                return null;

            _vmCache.AddOrUpdate(vm.Id, vm, (k, v) => v = vm);

            return vm;
        }

        private async Task<Vm[]> ReloadVmCache()
        {
            List<string> existing = _vmCache.Values
                .Select(o => o.Id)
                .ToList();

            List<Vm> list = new List<Vm>();

            var pveVms = await _pveClient.GetVmsAsync();

            //iterate through the collection of Vm's
            foreach (var pveVm in pveVms)
            {
                Vm vm = LoadVm(pveVm);

                if (vm != null)
                {
                    list.Add(vm);
                }
            }

            List<string> active = list.Select(o => o.Id).ToList();
            _logger.LogDebug($"refreshing cache [{_config.Host}] existing: {existing.Count} active: {active.Count}");

            foreach (string key in existing.Except(active))
            {
                if (_vmCache.TryRemove(key, out Vm stale))
                {
                    _logger.LogDebug($"removing stale cache entry [{_config.Host}] {stale.Name}");
                }
            }

            //return an array of vm's
            return list.ToArray();
        }

        private async Task MonitorSession()
        {
            _logger.LogDebug($"{_config.Host}: starting cache loop");
            await _vlanManager.Initialize();
            int step = 0;

            while (true)
            {
                try
                {
                    await ReloadVmCache();
                    if (step == 0)
                    {
                        await _vlanManager.Clean(_vmCache);
                        await DeleteUnusedTemplates();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, $"Failed to refresh cache for {_config.Host}");
                }
                finally
                {
                    await Task.Delay(_syncInterval);
                }

                step = (step + 1) % 2;
            }
            // _logger.LogDebug("sessionMonitor ended.");
        }

        private async Task DeleteUnusedTemplates()
        {
            var tasks = new List<Task>();

            foreach (var taggedVm in _vmCache)
            {
                if (taggedVm.Value.Tags.Contains(deleteTag))
                {
                    _logger.LogInformation($"Deleting vm with deleteTag: {taggedVm.Value?.Name} ({taggedVm.Key})");
                    tasks.Add(this.Delete(taggedVm.Key));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        private async Task MonitorTasks()
        {
            _logger.LogDebug($"{_config.Host}: starting task monitor");
            while (true)
            {
                try
                {
                    foreach (string key in _tasks.Keys.ToArray())
                    {
                        var t = _tasks[key];
                        var info = await _pveClient.Nodes[PveClientBase.GetNodeFromTask(t.Id)]
                            .Tasks[t.Id]
                            .Status
                            .ReadTaskStatus();

                        var nodeTask = info.ToModel<NodeTask>();

                        switch (nodeTask.Status)
                        {
                            case "running":
                                var taskLog = await _pveClient
                                    .Nodes[PveClientBase.GetNodeFromTask(t.Id)]
                                    .Tasks[t.Id]
                                    .Log
                                    .ReadTaskLog(start: t.LastLine);

                                var log = new PveNodeTaskLog(taskLog);
                                t.SetProgress(log);
                                break;

                            case "stopped":
                                t.SetProgress(info);
                                //_tasks.Remove(key);
                                break;

                            default:
                                t.SetProgress(info);
                                //_tasks.Remove(key);
                                break;
                        }

                        if (_vmCache.ContainsKey(key))
                        {
                            Vm vm = _vmCache[key];
                            if (vm.Task == null)
                                vm.Task = new VmTask();
                            vm.Task.Progress = t.Progress;
                            vm.Task.Name = t.Action;
                            _vmCache.TryUpdate(key, vm, vm);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, $"Error in task monitor of {_config.Host}");
                }
                finally
                {
                    await Task.Delay(_taskMonitorInterval);
                }
            }
            // _logger.LogDebug("taskMonitor ended.");
        }
    }
}
