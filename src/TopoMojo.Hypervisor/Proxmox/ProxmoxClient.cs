// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
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
using TopoMojo.Hypervisor.Exceptions;

namespace TopoMojo.Hypervisor.Proxmox
{
    public partial class ProxmoxClient
    {
        public ProxmoxClient(
            HypervisorServiceConfiguration options,
            ConcurrentDictionary<string, Vm> vmCache,
            ILogger<ProxmoxClient> logger,
            IProxmoxNameService nameService,
            IProxmoxVlanManager vnetService,
            Random random,
            IHttpClientFactory httpClientFactory
        )
        {
            _logger = logger;
            _config = options;
            _logger.LogDebug("Constructing Client {url}", _config.Url);
            _tasks = [];
            _vmCache = vmCache;
            _random = random;
            _httpClientFactory = httpClientFactory;
            _config.Tenant ??= "";

            int port = 443;
            string host = _config.Url;
            if (Uri.TryCreate(_config.Url, UriKind.RelativeOrAbsolute, out Uri result) && result.IsAbsoluteUri)
            {
                host = result.Host;
                port = result.Port;
            }
            _config.Host = host;
            _port = port;
            _hostPrefix = host.Split('.').FirstOrDefault();

            _pveClient = new PveClient(host, port, httpClientFactory.CreateClient("proxmox"))
            {
                ApiToken = options.AccessToken
            };

            _rootPveClient = new PveClient(host, port, httpClientFactory.CreateClient("proxmox"));

            _nameService = nameService;
            _vlanManager = vnetService;
            _ = MonitorSession();
            _ = MonitorTasks();
        }

        private readonly ILogger<ProxmoxClient> _logger;
        private readonly Dictionary<string, PveNodeTask> _tasks;
        private readonly ConcurrentDictionary<string, Vm> _vmCache;
        private readonly IProxmoxNameService _nameService;
        private readonly IProxmoxVlanManager _vlanManager;
        private readonly HypervisorServiceConfiguration _config = null;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly int _port;
        // int _pollInterval = 1000;
        readonly int _syncInterval = 30000;
        readonly int _taskMonitorInterval = 3000;
        readonly string _hostPrefix = "";
        // DateTimeOffset _lastAction;
        private readonly PveClient _pveClient;
        private readonly PveClient _rootPveClient;
        private readonly Random _random;
        private readonly object _lock = new();
        private const string deleteTag = "delete"; // tags are always lower-case

        public async Task<Vm> Refresh(VmTemplate template)
        {
            string target = $"{template.Name}#{template.IsolationTag}";
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
            // await ReloadVmCache();

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
                .Value
                ?? throw new InvalidOperationException("Parent Template does not exist");

            var nextId = await GetNextId();
            var pveId = int.Parse(nextId);
            var name = _nameService.ToPveName(template.Template);

            // full clone parent template
            var task = await _pveClient.Nodes[parentTemplate.Host].Qemu[parentTemplate.Id].Clone.CloneVm(
                pveId,
                full: true,
                name: name,
                target: parentTemplate.Host);
            await _pveClient.WaitForTaskToFinish(task);

            // convert new vm to template
            task = await _pveClient.Nodes[parentTemplate.Host].Qemu[nextId].Template.Template();
            await _pveClient.WaitForTaskToFinish(task);

            if (!string.IsNullOrEmpty(template.IsolationTag))
            {
                task = await _pveClient
                        .Nodes[parentTemplate.Host]
                        .Qemu[nextId]
                        .Config
                        .UpdateVmAsync(tags: template.IsolationTag);
                await _pveClient.WaitForTaskToFinish(task);
            }

            Vm vm = new()
            {
                Name = name,
                Id = nextId,
                State = VmPowerState.Off,
                Status = "deployed",
                Host = parentTemplate.Host,
                HypervisorType = HypervisorType.Proxmox
            };

            _vmCache.AddOrUpdate(vm.Id, vm, (k, v) => v = vm);

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
            var pveId = int.Parse(nextId);

            var cloneTask = _pveClient.Nodes[vmTemplate.Host].Qemu[vmTemplate.Id].Clone.CloneVm(
                pveId,
                full: false,
                name: _nameService.ToPveName(template.Name),
                target: targetNode);

            _logger.LogDebug("deploy: virtual networks (id {tid})...", template.Id);
            var vnetsTask = _vlanManager.Provision(template.Eth.Select(n => n.Net));
            var isoTask = GetIso(template);

            // We can clone vm and provision networks concurrently since we don't set the network until
            // the configure step after the clone is finished. Isos are also not dependent on the other tasks.
            try
            {
                await Task.WhenAll(cloneTask, vnetsTask, isoTask);
            }
            catch
            {
                if (cloneTask.IsFaulted)
                {
                    throw;
                }
            }

            task = cloneTask.Result;

            if (!vnetsTask.IsFaulted)
            {
                _logger.LogDebug("deploy: {count} networks deployed.", vnetsTask.Result.Count());
            }

            await _pveClient.WaitForTaskToFinish(task);

            if (isoTask.IsFaulted || vnetsTask.IsFaulted)
            {
                var exceptions = new List<Exception>();

                if (isoTask.IsFaulted)
                {
                    exceptions.Add(isoTask.Exception);
                }

                if (vnetsTask.IsFaulted)
                {
                    exceptions.Add(vnetsTask.Exception);
                }

                var destroyTask = await _pveClient.Nodes[targetNode].Qemu[nextId].DestroyVm();

                try
                {
                    await _pveClient.WaitForTaskToFinish(destroyTask);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }

                throw new AggregateException($"Exception deploying {template.Name}", exceptions);
            }

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

            var nics = await GetNics(template);
            var memory = GetMemory(template);
            var sockets = GetSockets(template);
            var coresPerSocket = GetCoresPerSocket(template);
            string args = null;

            if (setGuestSettings)
            {
                args = GetArgs(template);
            }

            task = await client.Nodes[targetNode].Qemu[nextId].Config.UpdateVmAsync(
                netN: nics,
                memory: memory,
                sockets: sockets,
                cores: coresPerSocket,
                cdrom: isoTask.Result,
                args: args);

            try
            {
                await _pveClient.WaitForTaskToFinish(task);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reconfiguring vm {name} ({nextid}) - {reason}", template.Name, nextId, task.ReasonPhrase);
                var destroyTask = await _pveClient.Nodes[targetNode].Qemu[nextId].DestroyVm();
                await _pveClient.WaitForTaskToFinish(destroyTask);

                throw;
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

            if (vm.Name.Contains('#').Equals(false) || vm.Name.ToTenant() != _config.Tenant)
                return null;

            _vmCache.AddOrUpdate(vm.Id, vm, (k, v) => v = vm);

            if (_config.EnableHA && !await RegisterHA(nextId, template.Name) && _config.RequireHA)
            {
                // the create call may have failed after the resource was created, and a vm cannot
                // be destroyed while it is an HA resource
                await UnregisterHA(nextId);
                _vmCache.TryRemove(vm.Id, out _);

                var destroyTask = await _pveClient.Nodes[targetNode].Qemu[nextId].DestroyVm();
                await _pveClient.WaitForTaskToFinish(destroyTask);

                throw new HypervisorException($"Could not register vm {template.Name} ({nextId}) as a Proxmox HA resource, and Pod__RequireHA is set.");
            }

            if (template.AutoStart)
            {
                _logger.LogDebug("deploy: start vm...");
                vm = await Start(vm.Id);
            }

            return vm;
        }

        public async Task<Vm> Start(string id)
        {
            Vm vm = _vmCache[id];

            // when a vm is HA managed, the HA manager owns its power state and will revert a
            // direct qemu start, so request the state change from it instead
            if (!_config.EnableHA || !await SetHAState(vm.Id, "started"))
            {
                var task = await _pveClient.Nodes[await GetCurrentNode(vm)].Qemu[vm.GetId()].Status.Start.VmStart();
                await _pveClient.WaitForTaskToFinish(task);
            }

            vm.State = VmPowerState.Running;

            _vmCache.TryUpdate(vm.Id, vm, vm);

            return vm;
        }

        public async Task<Vm> Stop(string id)
        {
            Vm vm = _vmCache[id];

            if (!_config.EnableHA || !await SetHAState(vm.Id, "stopped"))
            {
                var task = await _pveClient.Nodes[await GetCurrentNode(vm)].Qemu[vm.GetId()].Status.Stop.VmStop();
                await _pveClient.WaitForTaskToFinish(task);
            }

            vm.State = VmPowerState.Off;

            _vmCache.TryUpdate(vm.Id, vm, vm);

            return vm;
        }

        /// <summary>
        /// Resets a vm in place. Used instead of a stop/start pair for HA managed vms, where two
        /// state requests in quick succession would be collapsed into a no-op by the HA manager.
        /// </summary>
        public async Task<Vm> Reset(string id)
        {
            Vm vm = _vmCache[id];
            var node = await GetCurrentNode(vm);
            var status = await _pveClient.Nodes[node].Qemu[vm.GetId()].Status.Current.GetAsync();

            // a stopped vm can't be reset, so treat a reset of one as a start
            if (!status.IsRunning)
                return await Start(id);

            var task = await _pveClient.Nodes[node].Qemu[vm.GetId()].Status.Reset.VmReset();
            await _pveClient.WaitForTaskToFinish(task);
            vm.State = VmPowerState.Running;

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
            var node = await GetCurrentNode(vm);
            var status = await _pveClient.Nodes[node].Qemu[pveId].Status.Current.GetAsync();

            // a vm cannot be destroyed while it is an HA resource, so this has to come first
            if (_config.EnableHA)
            {
                await UnregisterHA(vm.Id);
            }

            if (status.IsRunning)
            {
                task = await _pveClient.ChangeStatusVmAsync(pveId, VmStatus.Stop);
                await _pveClient.WaitForTaskToFinish(task);
            }

            task = await _pveClient.Nodes[node].Qemu[id].DestroyVm();
            await _pveClient.WaitForTaskToFinish(task);
            vm.Status = "initialized";

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
            var node = await GetCurrentNode(vm);

            var result = await _pveClient.Nodes[node].Qemu[id].Vncproxy.Vncproxy(websocket: true);

            if (result.IsSuccessStatusCode)
            {
                string urlFragment = $"/api2/json/nodes/{node}/qemu/{id}/vncwebsocket?port={result.Response.data.port}&vncticket={WebUtility.UrlEncode(result.Response.data.ticket)}";
                url = $"wss://{_config.Host}{urlFragment}";
                ticket = result.Response.data.ticket;
            }
            else
            {
                throw new HypervisorException(result.GetError());
            }

            return new Tuple<string, string>(url, ticket);
        }

        public async Task<Vm> Save(string id)
        {
            Vm vm = _vmCache[id];

            if (vm != null)
            {
                var node = await GetCurrentNode(vm);
                var config = await _pveClient.Nodes[node].Qemu[vm.Id].Config.GetAsync();

                var disk = config.Disks.ElementAt(0);
                var storageItems = await _pveClient.Nodes[node].Storage[disk.Storage].Content.GetAsync();

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
                            var nextId = int.Parse(await GetNextId());
                            var task = await _pveClient.Nodes[node].Qemu[vm.Id].Clone.CloneVm(newid: nextId, full: true, target: template.Host, name: template.Name);

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

                // Convert to template
                task = await _pveClient.Nodes[template.Host].Qemu[nextId].Template.Template();

                try
                {
                    await _pveClient.WaitForTaskToFinish(task);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Convert to template failed, destroying.");
                    var destroyTask = await _pveClient.Nodes[template.Host].Qemu[nextId].DestroyVm();
                    await _pveClient.WaitForTaskToFinish(destroyTask);
                    throw;
                }

                // Delete old vm
                await Delete(oldId);

                // Tag old template
                // Janitor will delete anything with this tag if deletion fails now
                task = await _pveClient
                    .Nodes[template.Host]
                    .Qemu[template.Id]
                    .Config
                    .UpdateVmAsync(tags: deleteTag);
                await _pveClient.WaitForTaskToFinish(task);

                // delete old template
                task = await _pveClient.Nodes[template.Host].Qemu[template.Id].DestroyVm();
                await _pveClient.WaitForTaskToFinish(task);

                await ReloadVmCache();
            }
            finally
            {
                _tasks.Remove(vmId);
            }
        }

        public async Task<ProxmoxIsoFile[]> GetFiles()
        {
            var storage = ProxmoxIsoNaming.StorageName(_config.IsoStore);
            var node = await GetIsoStorageNode();

            var task = await _pveClient
                .Nodes[node]
                .Storage[storage]
                .Content
                .Index(content: "iso");
            await _pveClient.WaitForTaskToFinish(task);

            var isos = task.ToModel<PveIso[]>();

            return isos
                .Select(x => ProxmoxIsoFile.From(x, _config.IsoScopeSeparator))
                .ToArray();
        }


        private async Task<string> GetIsoStorageNode()
        {
            var storage = ProxmoxIsoNaming.StorageName(_config.IsoStore);
            var resources = await _pveClient.GetResourcesAsync(ClusterResourceType.Storage);
            return SelectIsoStorageNode(resources, storage, _random);
        }

        internal static string SelectIsoStorageNode(
            IEnumerable<ClusterResource> resources,
            string storage,
            Random random)
        {
            var candidates = resources
                .Where(x => x.ResourceType == ClusterResourceType.Storage
                    && string.Equals(x.Storage, storage, StringComparison.Ordinal)
                    && x.IsAvailable
                    && !string.IsNullOrWhiteSpace(x.Node))
                .ToList();
            var nodes = candidates
                .Select(x => x.Node)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (nodes.Count == 0)
                throw new HypervisorException($"No online Proxmox node currently offers ISO storage '{storage}'.");

            if (nodes.Count == 1)
                return nodes[0];

            if (!candidates.All(x => x.Shared))
            {
                throw new HypervisorException(
                    $"Pod__IsoStore '{storage}' is available on multiple nodes but is not shared; "
                    + "an ISO written through one node would not be mountable from the others.");
            }

            return nodes[random.Next(nodes.Count)];
        }

        public async Task UploadIso(string scopeId, string fileName, string localFilePath, CancellationToken cancellationToken = default)
        {
            var storage = ProxmoxIsoNaming.StorageName(_config.IsoStore);
            var storedName = ProxmoxIsoNaming.Encode(scopeId, fileName, _config.IsoScopeSeparator);

            if (!storedName.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)
                && !storedName.EndsWith(".img", StringComparison.OrdinalIgnoreCase))
            {
                throw new HypervisorException($"Proxmox ISO filename must end with .iso or .img: {storedName}");
            }

            var node = await GetIsoStorageNode();
            _logger.LogInformation(
                "Uploading Proxmox ISO {storedName} to storage {storage} on node {node}",
                storedName,
                storage,
                node);

            using var fileStream = File.OpenRead(localFilePath);
            var uploadClient = CreateIsoUploadClient();
            var result = await uploadClient.UploadFileToStorageAsync(
                node,
                storage,
                "iso",
                fileStream,
                storedName,
                cancellationToken,
                secondsTimeout: Math.Max(1, _config.IsoUploadTimeoutMinutes) * 60);

            var timeoutMs = (long)Math.Max(1, _config.IsoUploadTimeoutMinutes) * 60 * 1000;
            if (!await _pveClient.WaitForTaskToFinish(result, timeout: timeoutMs))
                throw new HypervisorException($"Proxmox ISO upload task did not finish within {Math.Max(1, _config.IsoUploadTimeoutMinutes)} minutes: {storedName}");

            _logger.LogInformation(
                "Uploaded Proxmox ISO {storedName} to storage {storage} on node {node}",
                storedName,
                storage,
                node);
        }

        public async Task DeleteIso(string scopeId, string fileName)
        {
            var storage = ProxmoxIsoNaming.StorageName(_config.IsoStore);
            var isos = await GetFiles();
            string[] candidates;

            try
            {
                candidates =
                [
                    ProxmoxIsoNaming.Encode(scopeId, fileName, _config.IsoScopeSeparator),
                    ProxmoxIsoNaming.EncodeLegacy(scopeId, fileName)
                ];
            }
            catch (ArgumentException ex)
            {
                throw new FileNotFoundException($"ISO not found in Proxmox storage: {scopeId}/{fileName}", ex);
            }

            var match = candidates
                .Select(name => isos.FirstOrDefault(x =>
                    string.Equals(x.Name, name, StringComparison.Ordinal)))
                .FirstOrDefault(x => x is not null);

            if (match is null)
                throw new FileNotFoundException($"ISO not found in Proxmox storage: {scopeId}/{fileName}");

            var node = await GetIsoStorageNode();
            _logger.LogInformation(
                "Deleting Proxmox ISO {volid} from storage {storage} on node {node}",
                match.Volid,
                storage,
                node);

            var result = await _pveClient
                .Nodes[node]
                .Storage[storage]
                .Content[match.Volid]
                .Delete();
            if (!await _pveClient.WaitForTaskToFinish(result))
                throw new HypervisorException($"Proxmox ISO delete task did not finish: {match.Volid}");

            _logger.LogInformation(
                "Deleted Proxmox ISO {volid} from storage {storage} on node {node}",
                match.Volid,
                storage,
                node);
        }

        // Use a per-upload client so the request timeout can differ without mutating the shared client.
        private PveClient CreateIsoUploadClient()
            => new(_config.Host, _port, _httpClientFactory.CreateClient("proxmox"))
            {
                ApiToken = _config.AccessToken
            };

        public async Task<PveVmConfig> GetVmConfig(Vm vm)
            => await GetVmConfig(await GetCurrentNode(vm), vm.GetId());

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
            var nicDataRegex = NicDataRegex();
            var nicModelMacRegex = NicModelMacRegex();

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
            var node = await GetCurrentNode(vm);
            var currentConfig = await GetVmConfig(node, vm.GetId());

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
                        ?? throw new HypervisorException($"Couldn't resolve an ID for virtual network {netUpdate.Value}");

                    var nic = currentConfig.Nics.SingleOrDefault(n => n.Index == netUpdate.Key)
                        ?? throw new HypervisorException($"Couldn't resolve a NIC on the host machine with index {netUpdate.Key}.");

                    var updateValue = $"{nic.PveModel}={nic.MacAddress},bridge={vnet.Vnet}";
                    vnetAssignmentIds.Add(netUpdate.Key, updateValue);
                }
            }

            var updateTask = await _pveClient
                .Nodes[node]
                .Qemu[vm.Id]
                .Config
                .UpdateVmAsync
                (
                    netN: vnetAssignmentIds.Count != 0 ? vnetAssignmentIds : null
                );

            await _pveClient.WaitForTaskToFinish(updateTask);
            return vm;
        }

        /// <summary>
        /// The HA resource id ("sid") of a vm, e.g. vm:100.
        /// </summary>
        private static string GetSid(string id) => $"vm:{id}";

        /// <summary>
        /// Registers a vm as a cluster HA resource so that Proxmox's Cluster Resource Scheduler can
        /// place and rebalance it. Resources are created stopped; power state is then driven through
        /// the HA manager (see <see cref="SetHAState"/>), because the HA manager reverts direct
        /// qemu power operations on resources it manages.
        /// </summary>
        /// <remarks>
        /// A failure is logged rather than thrown, and the caller decides what to do with it. An
        /// unmanaged vm still works; it just won't be rebalanced. See RequireHA.
        /// </remarks>
        /// <returns>False if the vm could not be registered.</returns>
        private async Task<bool> RegisterHA(string id, string comment)
        {
            try
            {
                var task = await _pveClient.Cluster.Ha.Resources.Create(
                    GetSid(id),
                    auto_rebalance: _config.HaAutoRebalance,
                    comment: comment,
                    max_relocate: _config.HaMaxRelocate,
                    max_restart: _config.HaMaxRestart,
                    state: "stopped",
                    type: "vm");
                await _pveClient.WaitForTaskToFinish(task);

                _logger.LogDebug("registered ha resource {sid} (auto_rebalance: {rebalance})", GetSid(id), _config.HaAutoRebalance);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register ha resource {sid}. The vm will not be managed by the Proxmox HA manager or rebalanced by CRS.", GetSid(id));

                return false;
            }
        }

        /// <summary>
        /// Removes a vm from cluster HA management. Purging also removes it from any HA rule that
        /// references it, deleting the rule if it was the rule's only resource.
        /// </summary>
        /// <returns>False if the vm was not an HA resource.</returns>
        private async Task<bool> UnregisterHA(string id)
        {
            try
            {
                var task = await _pveClient.Cluster.Ha.Resources[GetSid(id)].Delete(purge: true);
                await _pveClient.WaitForTaskToFinish(task);

                return true;
            }
            catch (Exception ex)
            {
                // most likely the vm was deployed before HA was enabled, so there is nothing to remove
                _logger.LogDebug(ex, "Could not delete ha resource {sid}", GetSid(id));
                return false;
            }
        }

        /// <summary>
        /// Requests a power state of the HA manager for a vm it manages.
        /// </summary>
        /// <param name="state">started or stopped</param>
        /// <returns>False if the vm was not an HA resource, so the caller can fall back to a direct qemu operation.</returns>
        private async Task<bool> SetHAState(string id, string state)
        {
            try
            {
                var task = await _pveClient.Cluster.Ha.Resources[GetSid(id)].Update(state: state);
                await _pveClient.WaitForTaskToFinish(task);

                return true;
            }
            catch (Exception ex)
            {
                // most likely the vm was deployed before HA was enabled
                _logger.LogWarning(ex, "Could not request ha state {state} for {sid}, falling back to a direct power operation", state, GetSid(id));
                return false;
            }
        }

        /// <summary>
        /// Keeps the vms of an isolation on the same node using a positive HA resource-affinity rule.
        /// Requires the vms to be HA resources, so it is only meaningful when EnableHA is set.
        /// </summary>
        /// <remarks>The rule is cleaned up by <see cref="UnregisterHA"/> when the last member vm is deleted.</remarks>
        public async Task SetPositiveAffinity(string isolationTag, Vm[] vms)
        {
            if (vms.Length == 0)
                return;

            var rule = GetAffinityRuleId(isolationTag);
            var existing = await GetAffinityRuleResources(rule);

            // vms of an isolation can be deployed one at a time, so add to the rule rather than replace it
            var sids = existing
                .Union(vms.Select(vm => GetSid(vm.Id)))
                .ToArray();

            if (sids.Length < 2)
            {
                _logger.LogDebug("skipping ha affinity {rule}, an isolation of one vm has nothing to keep together", rule);
                return;
            }

            var resources = string.Join(',', sids);

            var task = existing.Length != 0
                ? await _pveClient.Cluster.Ha.Rules[rule].UpdateRule("resource-affinity", resources: resources)
                : await _pveClient.Cluster.Ha.Rules.CreateRule(
                    resources,
                    rule,
                    "resource-affinity",
                    affinity: "positive",
                    comment: $"topomojo isolation {isolationTag}");

            await _pveClient.WaitForTaskToFinish(task);

            _logger.LogDebug("set positive ha affinity {rule} for {resources}", rule, resources);
        }

        /// <summary>
        /// The resource ids already in an HA rule, or an empty array if the rule doesn't exist.
        /// </summary>
        private async Task<string[]> GetAffinityRuleResources(string rule)
        {
            try
            {
                var result = await _pveClient.Cluster.Ha.Rules[rule].ReadRule();

                if (!result.IsSuccessStatusCode)
                    return [];

                return (((string)result.Response.data.resources) ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not read ha rule {rule}", rule);
                return [];
            }
        }

        /// <summary>
        /// Proxmox config ids allow only letters, numbers, dashes, underscores and periods.
        /// </summary>
        private static string GetAffinityRuleId(string isolationTag)
            => $"topomojo-{InvalidPveIdCharsRegex().Replace(isolationTag ?? "", "-")}";

        /// <summary>
        /// Resolves the node a vm is currently running on. CRS can migrate HA managed vms at any time,
        /// and the vm cache only catches up on its next refresh, so the cached node can be stale.
        /// </summary>
        private async Task<string> GetCurrentNode(Vm vm)
        {
            if (!_config.EnableHA)
                return vm.Host;

            try
            {
                var pveVm = await _pveClient.GetVmAsync(vm.Id);

                if (pveVm != null && !string.IsNullOrEmpty(pveVm.Node) && pveVm.Node != vm.Host)
                {
                    _logger.LogDebug("vm {name} ({id}) moved from {old} to {new}", vm.Name, vm.Id, vm.Host, pveVm.Node);
                    vm.Host = pveVm.Node;
                    _vmCache.TryUpdate(vm.Id, vm, vm);
                }

                return vm.Host;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not resolve the current node of vm {id}, using {host}", vm.Id, vm.Host);
                return vm.Host;
            }
        }

        /// <summary>
        /// Selects a Node to deploy to. Randomly picks among all online nodes with less than 50% memory usage, or the
        /// Node with the least memory usage if none are less than 50%.
        /// </summary>
        /// <returns></returns>
        private async Task<string> GetTargetNode()
        {
            string target = null;
            var nodes = await _pveClient.GetNodesAsync();

            if (nodes.Any())
            {
                IClusterResourceNode targetNode;
                var targetNodes = nodes.Where(x =>
                    x.IsOnline &&
                    x.MemoryUsagePercentage <= 50);

                if (targetNodes.Any())
                {
                    targetNode = targetNodes.ElementAt(_random.Next(0, targetNodes.Count()));
                }
                else
                {
                    targetNode = nodes
                        .OrderBy(x => x.MemoryUsagePercentage)
                        .Where(x => x.IsOnline)
                        .FirstOrDefault();
                }

                target = targetNode.Node;
            }

            return target;
        }

        private async Task<string> GetRandomNode()
        {
            var nodes = await _pveClient.GetNodesAsync();
            var randomNum = _random.Next(0, nodes.Count());
            return nodes.ElementAt(randomNum).Node;
        }

        private static string GetArgs(VmTemplate template)
        {
            if (template.GuestSettings.Length == 0)
                return null;

            var args = new StringBuilder();

            // Default settings
            // TODO: Fix duplication with vsphere?
            args.Append($"-fw_cfg name=opt/guestinfo.isolationTag,string=\"{template.IsolationTag}\" ");
            args.Append($"-fw_cfg name=opt/guestinfo.templateSource,string=\"{template.Id}\" ");
            args.Append($"-fw_cfg name=opt/guestinfo.hostname,string=\"{template.Name.Untagged()}\" ");

            foreach (var setting in template.GuestSettings)
            {
                // TODO: rework this quick fix for injecting isolation specific settings
                if (setting.Key.StartsWith("iftag.") && !setting.Value.Contains(template.IsolationTag))
                {
                    continue;
                }

                args.Append($"-fw_cfg name=opt/{setting.Key},string=\"{setting.Value}\" ");
            }

            return args.ToString().TrimEnd();
        }

        private async Task<string> GetIso(VmTemplate template)
        {
            if (string.IsNullOrEmpty(template.Iso)) return null;

            var match = MatchIso(await GetFiles(), template.Iso);

            if (match is null)
                _logger.LogWarning("No ISO matching {iso} on storage {storage}", template.Iso, _config.IsoStore);

            return match?.Volid;
        }

        // Matching goes through ProxmoxIsoFile.DisplayName only: ProxmoxIsoNaming.TryDecode already
        // resolves both the configured and the legacy '#' scope separator.
        internal static ProxmoxIsoFile MatchIso(IEnumerable<ProxmoxIsoFile> isos, string iso)
            => isos.FirstOrDefault(x =>
                string.Equals(x.DisplayName, iso, StringComparison.OrdinalIgnoreCase));

        private static string GetMemory(VmTemplate template)
        {
            return ((template.Ram > 0) ? template.Ram * 1024 : 1024).ToString();
        }

        private static int? GetCoresPerSocket(VmTemplate template)
        {
            string[] p = template.Cpu.Split('x');
            int coresPerSocket = 1;

            if (p.Length > 1)
            {
                if (!int.TryParse(p[1], out coresPerSocket))
                {
                    coresPerSocket = 1;
                }
            }

            return coresPerSocket;
        }

        private static int? GetSockets(VmTemplate template)
        {
            string[] p = template.Cpu.Split('x');
            if (!int.TryParse(p[0], out int sockets))
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
            Dictionary<int, string> nics = [];

            if (template.Eth.IsEmpty())
                return nics;

            var vnets = await _vlanManager.GetVnets();

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
            Vm vm = new()
            {
                Name = pveVm.Name == null ? "" : _nameService.FromPveName(pveVm.Name),
                Id = pveVm.VmId.ToString(),
                State = pveVm.IsRunning ? VmPowerState.Running : VmPowerState.Off,
                Status = "deployed",
                Host = pveVm.Node,
                Tags = pveVm.Tags == null ? [] : pveVm.Tags.Split(' '),
                HypervisorType = HypervisorType.Proxmox,
                IsTemplate = pveVm.IsTemplate
            };

            if (_tasks.TryGetValue(vm.Id, out PveNodeTask value))
            {
                var t = value;
                vm.Task = new VmTask { Name = t.Action, WhenCreated = t.WhenCreated, Progress = t.Progress };
            }

            // Proxmox Vm names are null for a few seconds when first deployed.
            // We still want to add to cache when in this state.
            if (!pveVm.IsTemplate && !string.IsNullOrEmpty(vm.Name) && vm.Name.Contains('#').Equals(false) ||
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

            List<Vm> list = [];

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
            _logger.LogDebug("refreshing cache [{host}] existing: {existing} active: {active}", _config.Host, existing.Count, active.Count);

            foreach (string key in existing.Except(active))
            {
                if (_vmCache.TryRemove(key, out Vm stale))
                {
                    _logger.LogDebug("removing stale cache entry [{host}] {stale}", _config.Host, stale.Name);
                }
            }

            //return an array of vm's
            return [.. list];
        }

        private async Task MonitorSession()
        {
            _logger.LogDebug("{host}: starting cache loop", _config.Host);

            await InitializeVlanManager();

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
                    _logger.LogError(ex, $"Failed to refresh cache for {_config.Host}");
                }

                await Task.Delay(_syncInterval);
                step = (step + 1) % 2;
            }
        }

        private async Task InitializeVlanManager()
        {
            while (true)
            {
                try
                {
                    await _vlanManager.Initialize();
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to initialize Proxmox VlanManager");
                    await Task.Delay(_syncInterval);
                }
            }
        }

        private async Task DeleteUnusedTemplates()
        {
            var tasks = new List<Task>();

            foreach (var taggedVm in _vmCache)
            {
                if (taggedVm.Value.Tags.Contains(deleteTag))
                {
                    _logger.LogInformation("Deleting vm with deleteTag: {name} ({key})", taggedVm.Value?.Name, taggedVm.Key);
                    tasks.Add(Delete(taggedVm.Key));
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        private async Task MonitorTasks()
        {
            _logger.LogDebug("{host}: starting task monitor", _config.Host);
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

                        if (_vmCache.TryGetValue(key, out Vm value))
                        {
                            Vm vm = value;
                            vm.Task ??= new VmTask();
                            vm.Task.Progress = t.Progress;
                            vm.Task.Name = t.Action;
                            _vmCache.TryUpdate(key, vm, vm);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(0, ex, "Error in task monitor of {host}", _config.Host);
                }
                finally
                {
                    await Task.Delay(_taskMonitorInterval);
                }
            }
            // _logger.LogDebug("taskMonitor ended.");
        }

        [GeneratedRegex(@"[^A-Za-z0-9_.\-]")]
        private static partial Regex InvalidPveIdCharsRegex();

        [GeneratedRegex(@"net(\d)+")]
        private static partial Regex NicDataRegex();

        [GeneratedRegex(@"(?<model>[^=]+)=(?<mac>[0-9A-Fa-f:]{17})")]
        private static partial Regex NicModelMacRegex();
    }
}
