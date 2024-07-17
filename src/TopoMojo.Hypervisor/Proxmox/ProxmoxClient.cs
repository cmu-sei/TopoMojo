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
using System.Text;
using Corsinvest.ProxmoxVE.Api.Shared.Utils;

namespace TopoMojo.Hypervisor.Proxmox
{
    public class ProxmoxClient
    {
        public ProxmoxClient(
            HypervisorServiceConfiguration options,
            ConcurrentDictionary<string, Vm> vmCache,
            VlanManager networkManager,
            ILogger<ProxmoxClient> logger,
            Random random
        )
        {
            _logger = logger;
            _config = options;
            _logger.LogDebug($"Constructing Client {_config.Host}");
            //_tasks = new Dictionary<string, VimHostTask>();
            _vmCache = vmCache;
            //_pgAllocation = new Dictionary<string, PortGroupAllocation>();
            _vlanman = networkManager;
            _hostPrefix = _config.Host.Split('.').FirstOrDefault();
            _random = random;

            if (_config.Tenant == null)
                _config.Tenant = "";

            _pveClient = new PveClient(options.Host, 443)
            {
                ApiToken = options.Password
            };

            Task sessionMonitorTask = MonitorSession();
        }

        private readonly VlanManager _vlanman;
        private readonly ILogger<ProxmoxClient> _logger;
        private ConcurrentDictionary<string, Vm> _vmCache;
        //private INetworkManager _netman;
        HypervisorServiceConfiguration _config = null;
        int _pollInterval = 1000;
        int _syncInterval = 30000;
        int _taskMonitorInterval = 3000;
        string _hostPrefix = "";
        DateTimeOffset _lastAction;
        private readonly PveClient _pveClient;
        private readonly Random _random;
        private readonly bool _enableHA = false;
        private readonly Object _lock = new object();

        public async Task DeletAll(string term)
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

            await this.CleanupNetworks(term);
        }

        public async Task<Vm> Refresh(VmTemplate template)
        {
            var resources = await _pveClient.GetResourcesAsync(ClusterResourceType.Vm);

            var pveVm = resources.Where(x => x.Name == ToPveName(template.Name)).FirstOrDefault();

            if (pveVm != null)
            {
                return new Vm
                {
                    Name = FromPveName(pveVm.Name),
                    Id = pveVm.VmId.ToString(),
                    State = pveVm.IsRunning ? VmPowerState.Running : VmPowerState.Off
                };
            }
            else
            {
                return null;
            }
        }

        public async Task<Vm> Deploy(VmTemplate template)
        {
            Result task;
            Vm vm = null;

            _logger.LogDebug("deploy: validate portgroups...");
            await this.Provision(template);
            //await _netman.Provision(template);

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
                name: ToPveName(template.Name),
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
                var pveVm = await _pveClient.GetVmAsync(ToPveName(nameOrId));

                vm = new Vm
                {
                    Name = FromPveName(pveVm.Name),
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
                        Name = FromPveName(pveVm.Name),
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
                    var parent = storageItems.Where(x => x.FileName == pveDisk.Parent.Split('@')[0]).FirstOrDefault();

                    if (parent != null && parent.Parent == null)
                    {
                        // check if anything else is using the template
                        var count = storageItems.Where(x => x.Parent != null && x.Parent.Split('@')[0] == parent.FileName).Count();

                        if (count > 1)
                        {
                            throw new InvalidOperationException("Base Template is in use");
                        }

                        // find template (filename example: base-100-disk-0 where 100 is the templateId)
                        var templateId = parent.FileName.Split('-')[1];
                        var template = pveVms.Where(x => x.VmId.ToString() == templateId).FirstOrDefault();

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
                        }
                    }
                }
            }

            return new Vm
            {
                Status = "created",
                Id = null
            };
        }

        public async Task<PveIso[]> GetFiles()
        {
            var node = await this.GetRandomNode();

            var task = await _pveClient.Nodes[node].Storage["nfs"].Content.Index(content: "iso");
            await this.WaitForTaskToFinish(task);

            var isos = task.ToModel<PveIso[]>();

            return isos;
        }

        private string ToPveName(string vmName)
        {
            return vmName.Replace("#", "--");
        }

        private string FromPveName(string vmName)
        {
            return vmName.Replace("--", "#");
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

        private async Task Provision(VmTemplate template)
        {
            lock (_lock)
            {
                var task = _pveClient.Cluster.Sdn.Vnets.Index().Result;
                var vnets = task.ToModel<PveVnet[]>();

                bool addedNets = false;

                foreach (var eth in template.Eth)
                {
                    if (vnets.Where(x => x.Alias == this.ToPveName(eth.Net)).Any())
                    {
                        continue;
                    }

                    int? vnet = null;

                    while (vnet == null)
                    {
                        var vnetId = _random.Next(100, 100000);

                        if (!vnets.Where(x => x.Tag == vnetId).Any())
                        {
                            vnet = vnetId;
                        }
                    }

                    task = _pveClient.Cluster.Sdn.Vnets.Create(
                        vnet: this.GetRandomVnetId(),
                        tag: vnet,
                        zone: _config.SDNZone,
                        alias: this.ToPveName(eth.Net)).Result;
                    this.WaitForTaskToFinish(task).Wait();

                    if (task.IsSuccessStatusCode)
                    {
                        addedNets = true;
                    }
                }

                if (addedNets)
                {
                    task = _pveClient.Cluster.Sdn.Reload().Result;
                    this.WaitForTaskToFinish(task).Wait();
                }
            }
        }

        public async Task CleanupNetworks(string term)
        {
            var task = await _pveClient.Cluster.Sdn.Vnets.Index();
            var vnets = task.ToModel<PveVnet[]>();

            var success = true;
            var any = false;

            foreach (var vnet in vnets.Where(x => x.Alias.Contains(term)))
            {
                any = true;
                task = await _pveClient.Cluster.Sdn.Vnets[vnet.Vnet].Delete();
                await this.WaitForTaskToFinish(task);

                if (!task.IsSuccessStatusCode)
                {
                    success = false;
                }
            }

            if (any)
            {
                task = await _pveClient.Cluster.Sdn.Reload();
                await this.WaitForTaskToFinish(task);

                if (!task.IsSuccessStatusCode)
                {
                    success = false;
                }
            }

            if (!success)
            {
                throw new Exception($"Exception cleaning up networks for {term}");
            }
        }

        private static readonly char[] _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        private string GetRandomVnetId()
        {
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i <= 7; i++)
            {
                builder.Append(_chars[_random.Next(_chars.Length)]);
            }

            return builder.ToString();
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

                var vnet = vnets.Where(x => x.Alias == this.ToPveName(eth.Net)).FirstOrDefault();
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
                Name = FromPveName(pveVm.Name),
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
                .Where(v => v.Host == _config.Host)
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