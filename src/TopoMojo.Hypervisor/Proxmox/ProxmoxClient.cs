// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using Corsinvest.ProxmoxVE.Api;
using Corsinvest.ProxmoxVE.Api.Extension;
using System.Threading.Tasks;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Cluster;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Vm;
using System.Net;
using System.Collections.Generic;
using TopoMojo.Hypervisor.Proxmox.Models;
using TopoMojo.Hypervisor.Extensions;

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
            _vmCache = vmCache;
            _hostPrefix = _config.Host.Split('.').FirstOrDefault();
            _random = random;

            if (_config.Tenant == null)
                _config.Tenant = "";

            _pveClient = new PveClient(options.Host, 443)
            {
                ApiToken = options.Password
            };

            _nameService = nameService;
            _vlanManager = vnetService;
            Task sessionMonitorTask = MonitorSession();
        }

        private readonly ILogger<ProxmoxClient> _logger;
        private ConcurrentDictionary<string, Vm> _vmCache;
        private readonly IProxmoxNameService _nameService;
        private readonly IProxmoxVlanManager _vlanManager;
        private readonly HypervisorServiceConfiguration _config = null;
        // int _pollInterval = 1000;
        int _syncInterval = 30000;
        // int _taskMonitorInterval = 3000;
        string _hostPrefix = "";
        // DateTimeOffset _lastAction;
        private readonly PveClient _pveClient;
        private readonly Random _random;
        private readonly bool _enableHA = false;
        private readonly Object _lock = new object();

        public async Task DeleteAll(string term)
        {
            var tasks = new List<Task>();
            var pveVms = await _pveClient.GetVmsAsync();

            foreach (var pveVm in pveVms)
            {
                if (pveVm.Name.Contains(term))
                {
                    tasks.Add(this.Delete(pveVm.VmId.ToString()));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            await _vlanManager.DeleteVnetsByTerm(term);
        }

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
            await this.ReloadVmCache();

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

            // full clone parent template
            var task = await _pveClient.Nodes[parentTemplate.Host].Qemu[parentTemplate.Id].Clone.CloneVm(
                pveId,
                full: true,
                name: _nameService.ToPveName(template.Template),
                target: parentTemplate.Host);
            await _pveClient.WaitForTaskToFinishAsync(task);

            if (task.IsSuccessStatusCode)
            {
                // convert new vm to template
                task = await _pveClient.Nodes[parentTemplate.Host].Qemu[nextId].Template.Template();
                await this.WaitForTaskToFinish(task);
            }
            else
            {
                throw new Exception(task.ReasonPhrase);
            }

            await this.ReloadVmCache();

            return _vmCache
                .Where(x => x.Value.Name == template.Template)
                .FirstOrDefault()
                .Value;
        }

        public async Task<Vm> Deploy(VmTemplate template)
        {
            Result task;
            Vm vm = null;

            _logger.LogDebug($"deploy: virtual networks (id {template.Id})...");

            var vnets = await _vlanManager.Provision(template.Eth.Select(n => n.Net));
            _logger.LogDebug($"deploy: {vnets.Count()} networks deployed.");

            _logger.LogDebug("deploy: transform template...");
            //var transformer = new VCenterTransformer { DVSuuid = _dvsuuid };
            // VirtualMachineConfigSpec vmcs = Transform.TemplateToVmSpec(
            //     template,
            //     _config.VmStore.Replace("{host}", _hostPrefix),
            //     _dvsuuid
            // );

            _logger.LogDebug("deploy: create vm...");
            var targetNode = await GetTargetNode();
            var vmTemplate = _vmCache
                .Where(x => x.Value.Name == template.Template)
                .FirstOrDefault()
                .Value;

            var nextId = await GetNextId();
            var pveId = Int32.Parse(nextId);

            task = await _pveClient.Nodes[vmTemplate.Host].Qemu[vmTemplate.Id].Clone.CloneVm(
                pveId,
                full: false,
                name: _nameService.ToPveName(template.Name),
                target: targetNode);
            await _pveClient.WaitForTaskToFinishAsync(task);

            if (task.IsSuccessStatusCode)
            {
                var vmRef = _pveClient.Nodes[targetNode].Qemu[nextId];

                var nics = await this.GetNics(template);
                var memory = this.GetMemory(template);
                var sockets = this.GetSockets(template);
                var coresPerSocket = this.GetCoresPerSocket(template);
                var iso = await this.GetIso(template);

                task = await vmRef.Config.UpdateVmAsync(
                    netN: nics,
                    memory: memory,
                    sockets: sockets,
                    cores: coresPerSocket,
                    cdrom: iso);
                await this.WaitForTaskToFinish(task);

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
                    Host = targetNode
                };

                if (vm.Name.Contains("#").Equals(false) || vm.Name.ToTenant() != _config.Tenant)
                    return null;

                _vmCache.AddOrUpdate(vm.Id, vm, (k, v) => (v = vm));

                if (_enableHA)
                {
                    task = await _pveClient.Cluster.Ha.Resources.Create(nextId);
                }

                if (template.AutoStart && task.IsSuccessStatusCode)
                {
                    _logger.LogDebug("deploy: start vm...");
                    vm = await Start(vm.Id);
                }

                // bool ready = false;

                // while (!ready)
                // {
                //     var vmList = await _pveClient.GetVmsAsync();
                //     var pveVm = vmList.Where(x => x.VmId == Int32.Parse(nextId)).FirstOrDefault();

                //     if (pveVm != null && pveVm.Name != null)
                //     {
                //         ready = true;
                //     }

                //     if (!ready)
                //     {
                //         Console.WriteLine("Vm not ready, sleeping");
                //         await Task.Delay(3000);
                //     }
                // }
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
            await this.WaitForTaskToFinish(task);

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
            await this.WaitForTaskToFinish(task);

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

        public async Task<Vm> Delete(string id)
        {
            Result task;
            var pveId = long.Parse(id);
            Vm vm = _vmCache[id];
            var status = await _pveClient.Nodes[vm.Host].Qemu[pveId].Status.Current.GetAsync();

            if (_enableHA)
            {
                task = await _pveClient.Cluster.Ha.Resources[pveId].Delete();
                await this.WaitForTaskToFinish(task);
            }

            if (status.IsRunning)
            {
                task = await _pveClient.ChangeStatusVmAsync(pveId, VmStatus.Stop);
                await this.WaitForTaskToFinish(task);
            }

            task = await _pveClient.Nodes[vm.Host].Qemu[id].DestroyVm();
            await this.WaitForTaskToFinish(task);

            if (task.IsSuccessStatusCode)
            {
                //vm.Status = "created";
                //vm.Id = null;
                vm.Status = "initialized";
            }

            _vmCache.TryRemove(vm.Id, out vm);

            return vm;
        }

        public async Task<Vm> GetVm(string nameOrId)
        {
            Vm vm = null;

            try
            {
                var pveVm = await _pveClient.GetVmAsync(_nameService.ToPveName(nameOrId));

                vm = new Vm
                {
                    Name = _nameService.FromPveName(pveVm.Name),
                    Id = pveVm.VmId.ToString(),
                    State = pveVm.IsRunning ? VmPowerState.Running : VmPowerState.Off,
                    Status = "created"
                };
            }
            catch (Exception)
            {

            }

            return vm;
        }

        public async Task<Tuple<string, string>> GetTicket(string id)
        {
            string url;
            string ticket;
            var pveVm = await _pveClient.GetVmAsync(id);
            var vmRef = _pveClient.Nodes[pveVm.Node].Qemu[id];

            var result = await vmRef.Vncproxy.Vncproxy(websocket: true);

            if (result.IsSuccessStatusCode)
            {
                string urlFragment = $"/api2/json/nodes/{pveVm.Node}/{pveVm.Type.ToString().ToLower()}/{id}/vncwebsocket?port={result.Response.data.port}&vncticket={WebUtility.UrlEncode(result.Response.data.ticket)}";

                url = $"wss://{_config.Host}{urlFragment}";
                ticket = result.Response.data.ticket;
            }
            else
            {
                throw new Exception(result.GetError());
            }

            return new Tuple<string, string>(url, ticket);
        }

        public async Task<Vm[]> Find(string term)
        {
            var vms = new List<Vm>();
            var pveVms = await _pveClient.GetVmsAsync();

            foreach (var pveVm in pveVms)
            {
                if (pveVm.Name != null && pveVm.Name.Contains(term))
                {
                    vms.Add(new Vm
                    {
                        Name = _nameService.FromPveName(pveVm.Name),
                        Id = pveVm.VmId.ToString(),
                        State = pveVm.IsRunning ? VmPowerState.Running : VmPowerState.Off,
                    });
                }
            }

            return vms.ToArray();
        }

        public async Task<Vm> Save(string id)
        {
            var pveVms = await _pveClient.GetVmsAsync();
            var pveVm = pveVms.Where(x => x.VmId.ToString() == id).FirstOrDefault();

            if (pveVm != null)
            {
                var config = await _pveClient.Nodes[pveVm.Node].Qemu[pveVm.VmId].Config.GetAsync();

                var disk = config.Disks.ElementAt(0);
                var storageItems = await _pveClient.Nodes[pveVm.Node].Storage[disk.Storage].Content.GetAsync();

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

                        var template = pveVms.Where(x => x.VmId == parent.VmId).FirstOrDefault();

                        if (template != null && template.IsTemplate)
                        {
                            // Full Clone vm
                            var nextId = Int32.Parse(await this.GetNextId());
                            var task = await _pveClient.Nodes[pveVm.Node].Qemu[pveVm.VmId].Clone.CloneVm(newid: nextId, full: true, target: template.Node, name: template.Name);
                            await this.WaitForTaskToFinish(task);

                            if (!task.IsSuccessStatusCode)
                                throw new Exception($"Clone failed: {task.ReasonPhrase}");

                            // Delete old vm
                            await this.Delete(id);

                            // Convert to template
                            task = await _pveClient.Nodes[template.Node].Qemu[nextId].Template.Template();
                            await this.WaitForTaskToFinish(task);

                            if (!task.IsSuccessStatusCode)
                                throw new Exception($"Convert to template failed: {task.ReasonPhrase}");

                            // Rename old template
                            task = await _pveClient.Nodes[template.Node].Qemu[template.VmId].Config.UpdateVmAsync(name: $"{template.Name}-DELETEME");
                            await this.WaitForTaskToFinish(task);

                            if (!task.IsSuccessStatusCode)
                                throw new Exception($"Rename old template failed: {task.ReasonPhrase}");

                            // delete old template
                            task = await _pveClient.Nodes[template.Node].Qemu[template.VmId].DestroyVm();
                            await this.WaitForTaskToFinish(task);

                            if (!task.IsSuccessStatusCode)
                                throw new Exception($"Delete old template failed: {task.ReasonPhrase}");

                            await this.ReloadVmCache();
                        }
                    }
                }
            }

            return new Vm
            {
                Status = "initialized",
                Id = null
            };
        }

        public async Task<PveIso[]> GetFiles()
        {
            var node = await this.GetRandomNode();

            var task = await _pveClient
                .Nodes[node]
                .Storage[_config.IsoStore]
                .Content
                .Index(content: "iso");
            await this.WaitForTaskToFinish(task);

            var isos = task.ToModel<PveIso[]>();

            return isos;
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

        private async Task WaitForTaskToFinish(Result task)
        {
            try
            {
                await _pveClient.WaitForTaskToFinishAsync(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error waiting for task to finish");
            }
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
            int sockets = 1;

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

            task = _pveClient.Cluster.Sdn.Vnets.Index().Result;
            await this.WaitForTaskToFinish(task);
            var vnets = task.ToModel<PveVnet[]>();

            for (int i = 0; i < template.Eth.Length; i++)
            {
                var eth = template.Eth[i];

                var vnet = vnets.Where(x => x.Alias == _nameService.ToPveName(eth.Net)).FirstOrDefault();
                if (vnet != null)
                {
                    nics.Add(i, $"virtio,bridge={vnet.Vnet}");
                }
            }

            return nics;
        }

        private Vm LoadVm(IClusterResourceVm pveVm)
        {
            Vm vm = new Vm()
            {
                Name = _nameService.FromPveName(pveVm.Name),
                Id = pveVm.VmId.ToString(),
                State = pveVm.IsRunning ? VmPowerState.Running : VmPowerState.Off,
                Status = "deployed",
                Host = pveVm.Node
            };

            if (!pveVm.IsTemplate && vm.Name.Contains("#").Equals(false) || vm.Name.ToTenant() != _config.Tenant)
                return null;

            _vmCache.AddOrUpdate(vm.Id, vm, (k, v) => (v = vm));

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
            int step = 0;

            while (true)
            {
                try
                {
                    await ReloadVmCache();
                    if (step == 0)
                    {
                        //await _netman.Clean();
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
    }
}
