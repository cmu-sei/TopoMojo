// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.vSphere
{
    public class vSphereHypervisorService : IHypervisorService, IHostedService
    {
        public vSphereHypervisorService(
            HypervisorServiceConfiguration options,
            ILoggerFactory mill
        )
        {
            _options = options;
            _mill = mill;
            _logger = _mill.CreateLogger<vSphereHypervisorService>();
            _hostCache = new ConcurrentDictionary<string, VimClient>();
            _affinityMap = new Dictionary<string, VimClient>();
            _vmCache = new ConcurrentDictionary<string, Vm>();
            _vlanman = new VlanManager(_options.Vlan);

            NormalizeOptions(_options);
        }

        private readonly HypervisorServiceConfiguration _options;
        private readonly VlanManager _vlanman;

        private readonly ILogger<vSphereHypervisorService> _logger;
        private readonly ILoggerFactory _mill;
        private ConcurrentDictionary<string, VimClient> _hostCache;
        private DateTimeOffset _lastCacheUpdate = DateTimeOffset.MinValue;
        private Dictionary<string, VimClient> _affinityMap;
        private ConcurrentDictionary<string, Vm> _vmCache;

        public HypervisorServiceConfiguration Options { get {return _options;}}

        public async Task ReloadHost(string hostname)
        {
            string host = "https://" + hostname + "/sdk";
            await AddHost(host);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            InitHost(_options.Url);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var host in _hostCache.Values)
            {
                host.Disconnect().Wait();
            }
            return Task.CompletedTask;
        }

        // TODO: refactor this as InitializationProgress
        public async Task<Vm> Refresh(VmTemplate template)
        {
            string target = template.Name + "#" + template.IsolationTag;

            Vm vm = await Load(target);

            if (vm == null)
            {
                vm = new Vm() { Name = target, Status = "created" };

                int[] progress = await VerifyDisks(template);

                if (progress.Length == 0 || progress.Sum() == 100 * progress.Length)
                {
                    vm.Status = "initialized";
                }
                else if (progress.Sum() >= 0)
                {
                    vm.Task = new VmTask {
                        Name = "initializing",
                        Progress = progress.Sum() / progress.Length
                    };
                }
            }

            return vm;
        }
        public async Task<Vm> Deploy(VmTemplate template, bool privileged = false)
        {

            var vm = await Load(template.Name + "#" + template.IsolationTag);
            if (vm != null)
                return vm;

            VimClient host = FindHostByAffinity(template.IsolationTag);
            _logger.LogDebug("deploy: host " + host.Name);

            NormalizeTemplate(template, host.Options, privileged);
            _logger.LogDebug("deploy: normalized "+ template.Name);

            // ensure disks exists
            if (template.Disks.Any() && (await VerifyNormalizedDisks(template, host)).Any(i => i < 100))
                throw new Exception("Template disks have not been prepared.");

            if (!host.Options.IsNsxNetwork && !host.Options.Uplink.StartsWith("nsx."))
            {
                _logger.LogDebug("deploy: reserve vlans ");
                _vlanman.ReserveVlans(template, host.Options.IsVCenter);
            }

            _logger.LogDebug("deploy: " + template.Name + " " + host.Name);
            return await host.Deploy(template);
        }

        public async Task SetAffinity(string isolationTag, Vm[] vms, bool start)
        {
            _logger.LogDebug("setaffinity: find host ");
            VimClient host = FindHostByAffinity(isolationTag);

            _logger.LogDebug("setaffinity: setting affinity ");
            await host.SetAffinity(isolationTag, vms, start);
        }

        public async Task<Vm> Load(string id)
        {
            await Task.Delay(0);

            Vm vm = _vmCache.Values.Where(o=>o.Id == id || o.Name == id).FirstOrDefault();

            if (vm != null)
                CheckProgress(vm);

            return vm;
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

        public async Task<Vm> ChangeState(VmOperation op)
        {
            Vm vm = null;
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
                vm = await Save(op.Id);
                break;

                case VmOperationType.Revert:
                vm = await Revert(op.Id);
                break;

                case VmOperationType.Delete:
                vm = await Delete(op.Id);
                break;
            }
            return vm;
        }

        public async Task<Vm> Start(string id)
        {
            _logger.LogDebug("starting " + id);
            var ctx = GetVmContext(id);
            return await ctx.Host.Start(ctx.Vm.Id);
        }

        public async Task<Vm> Stop(string id)
        {
            _logger.LogDebug("stopping " + id);
            var ctx = GetVmContext(id);
            return await ctx.Host.Stop(ctx.Vm.Id);
        }

        public async Task<Vm> Save(string id)
        {
            _logger.LogDebug("saving " + id);
            var ctx = GetVmContext(id);
            return await ctx.Host.Save(ctx.Vm.Id);
        }

        public async Task<Vm> Revert(string id)
        {
            _logger.LogDebug("reverting " + id);
            var ctx = GetVmContext(id);
            return await ctx.Host.Revert(ctx.Vm.Id);
        }

        public async Task<Vm> Delete(string id)
        {
            _logger.LogDebug("deleting " + id);
            var ctx = GetVmContext(id);
            Vm vm =  await ctx.Host.Delete(ctx.Vm.Id);
            RefreshAffinity(); //TODO: fix race condition here
            return vm;
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
            var tasks = new List<Task>();
            foreach (var vm in await Find(target))
            {
                var ctx = GetVmContext(vm.Id);
                tasks.Add(ctx.Host.Delete(vm.Id));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }

        public async Task<Vm> ChangeConfiguration(string id, VmKeyValue change, bool sudo = false)
        {
            _logger.LogDebug("changing " + id + " " + change.Key + "=" + change.Value);

            var ctx = GetVmContext(id);

            var segments = change.Value.Split(':');
            string val = segments.First();
            string tag = segments.Length > 1
                ? $":{segments.Last()}"
                : "";

            //sanitize inputs
            if (change.Key == "iso")
            {
                // vmo = await GetVmIsoOptions(vm.Name.Tag());
                // if (!vmo.Iso.Contains(change.Value))
                //     throw new InvalidOperationException();
                var isopath = new DatastorePath(val);
                isopath.Merge(ctx.Host.Options.IsoStore);
                change = new VmKeyValue
                {
                    Key = "iso",
                    Value = isopath.ToString() + tag
                };
            }

            if (change.Key == "net" && !sudo && !change.Value.StartsWith("_none_"))
            {
                var vmo = await GetVmNetOptions(ctx.Vm.Name.Tag());
                if (!vmo.Net.Contains(val))
                    throw new InvalidOperationException();
            }

            return await ctx.Host.Change(ctx.Vm.Id, change);
        }

        public async Task<Vm[]> Find(string term)
        {
            await Task.Delay(0);

            IEnumerable<Vm> q = _vmCache.Values;

            if (term.HasValue())
                q =  q.Where(o=>o.Id.Contains(term) || o.Name.Contains(term));

            return CheckProgress(q.ToArray());
        }

        public async Task<int[]> VerifyDisks(VmTemplate template)
        {
            VimClient host = FindHostByRandom();

            NormalizeTemplate(template, host.Options);

            return await VerifyNormalizedDisks(template, host);
        }

        private async Task<int[]> VerifyNormalizedDisks(VmTemplate template, VimClient host)
        {
            var result = new int[template.Disks.Length];

            int index = 0;
            foreach (var disk in template.Disks)
            {
                // check running tasks
                foreach (VimClient vhost in _hostCache.Values)
                {
                    result[index] = await vhost.TaskProgress(disk.Path);
                    if (result[index] >= 0)
                        break;
                }

                // check file existence
                if (result[index] < 0)
                    result[index] = (await host.FileExists(disk.Path)) ? 100 : -1;

                index += 1;
            }

            return result;
        }

        public async Task<int> CreateDisks(VmTemplate template)
        {
            VimClient host = FindHostByRandom();

            NormalizeTemplate(template, host.Options);

            int[] progress = await VerifyNormalizedDisks(template, host);

            if (progress.Length == 0)
                return 100;

            int index = 0;
            foreach (var disk in template.Disks)
            {
                if (progress[index] >= 0)
                    continue;

                if (disk.Source.HasValue())
                {
                    await host.CloneDisk(disk.Source, disk.Path);
                    progress[index] = 0;
                } else {
                    await host.CreateDisk(disk);
                    progress[index] = 100;
                }
                index += 1;
            }

            return 0;
        }

        public async Task DeleteDisks(VmTemplate template)
        {
            int[] progress = await VerifyDisks(template);

            VimClient host = FindHostByRandom();

            int index = 0;
            foreach (VmDisk disk in template.Disks)
            {
                // skip missing and pending
                if (progress[index] < 100)
                    continue;

                // protect stock disks; only delete a disk if it is local to the workspace
                // i.e. the disk folder matches the workspaceId
                if (
                    string.IsNullOrEmpty(template.IsolationTag) ||
                    disk.Path.Contains(template.IsolationTag).Equals(false)
                ) continue;

                // delete disk
                await host.DeleteDisk(disk.Path);

                index += 1;
            }
        }

        public async Task<VmConsole> Display(string id)
        {
            var info = new VmConsole();

            try
            {
                var ctx = GetVmContext(id);

                info = new VmConsole
                {
                    Id = ctx.Vm.Id,
                    Name = ctx.Vm.Name.Untagged(),
                    IsolationId = ctx.Vm.Name.Tag(),
                    IsRunning = ctx.Vm.State == VmPowerState.Running
                };

                // throws if powered off
                info.Url = await ctx.Host.GetTicket(ctx.Vm.Id);

            }
            catch  {}

            return info;
        }

        public async Task<Vm> Answer(string id, VmAnswer answer)
        {
            var ctx = GetVmContext(id);
            return await ctx.Host.AnswerVmQuestion(ctx.Vm.Id, answer.QuestionId, answer.ChoiceKey);
        }

        public async Task<VmOptions> GetVmIsoOptions(string id)
        {
            VimClient host = FindHostByRandom();

            List<string> isos = new List<string>();

            string publicFolder = Guid.Empty.ToString();

            isos.AddRange(
                await host.GetFiles(host.Options.IsoStore + id + "/*.iso", false)
            );
            isos.AddRange(
                await host.GetFiles(host.Options.IsoStore + publicFolder + "/*.iso", false)
            );

            //translate actual path to display path
            isos = isos.Select(x => x.Replace(host.Options.IsoStore, "").Trim()).ToList();

            return new VmOptions {
                Iso = isos.ToArray()
            };
        }

        public async Task<VmOptions> GetVmNetOptions(string id)
        {
            await Task.Delay(0);
            return new VmOptions {
                Net = _vlanman.FindNetworks(id)
            };
        }
        public string Version
        {
            get
            {
                return "TopoMojo Pod Manager for vSphere, v1.0.0";
            }
        }

        private void NormalizeTemplate(VmTemplate template, HypervisorServiceConfiguration option, bool privileged = false)
        {
            if (!template.Iso.HasValue())
            {
                // need to have a backing file to add the cdrom device
                template.Iso = option.IsoStore + "null.iso";
            }

            var isopath = new DatastorePath(template.Iso);
            isopath.Merge(option.IsoStore);
            template.Iso = isopath.ToString();

            foreach (VmDisk disk in template.Disks)
            {
                if (!disk.Path.StartsWith(option.DiskStore)
                ) {
                    DatastorePath dspath = new DatastorePath(disk.Path);
                    dspath.Merge(option.DiskStore);
                    disk.Path = dspath.ToString();
                }

                if (disk.Source.HasValue() && !disk.Source.StartsWith(option.DiskStore)
                ) {
                    DatastorePath dspath = new DatastorePath(disk.Source);
                    dspath.Merge(option.DiskStore);
                    disk.Source = dspath.ToString();
                }
            }

            if (template.IsolationTag.HasValue())
            {
                string tag = "#" + template.IsolationTag;

                Regex rgx = new Regex("#.*");

                if (!template.Name.EndsWith(template.IsolationTag))
                    template.Name = rgx.Replace(template.Name, "") + tag;

                foreach (VmNet eth in template.Eth)
                {
                    if (privileged && _vlanman.Contains(eth.Net))
                        continue;

                    eth.Net = rgx.Replace(eth.Net, "") + tag;
                }
            }
        }

        private VmContext GetVmContext(string id)
        {
            var vm = _vmCache.Values.FirstOrDefault(v =>
                v.Id == id || v.Name == id
            );

            if (vm is null)
                throw new InvalidOperationException("ResourceNotFound");

            return new VmContext
            {
                Vm = vm,
                Host = _hostCache[vm.Host]
            };
        }

        private void RefreshAffinity()
        {
            lock(_affinityMap)
            {
                List<string> tags = new List<string>();
                foreach (Vm vm in _vmCache.Values)
                {
                    string tag = vm.Name.Tag();
                    tags.Add(tag);
                    if (!_affinityMap.ContainsKey(tag))
                        _affinityMap.Add(tag, _hostCache[vm.Host]);
                }
                string[] stale = _affinityMap.Keys.ToArray().Except(tags.Distinct().ToArray()).ToArray();
                foreach (string key in stale)
                    _affinityMap.Remove(key);
            }
        }

        private VimClient FindHostByAffinity(string tag)
        {
            VimClient host = null;
            lock(_affinityMap)
            {
                if (_affinityMap.ContainsKey(tag))
                    host =  _affinityMap[tag];
                else
                {
                    Vm vm = _vmCache.Values.Where(o=>o.Name.EndsWith(tag)).FirstOrDefault();
                    if (vm !=  null)
                        host = _hostCache[vm.Host];
                    else
                        host = FindHostByFewestVms();
                    _affinityMap.Add(tag, host);
                }
            }
            return host;
        }

        private VimClient FindHostByFewestVms()
        {
            Dictionary<string, HostVmCount> hostCounts = new Dictionary<string, HostVmCount>();
            foreach (VimClient host in _hostCache.Values)
            {
                if (!hostCounts.ContainsKey(host.Name))
                    hostCounts.Add(host.Name, new HostVmCount { Name = host.Name });
            }
            foreach (Vm vm in _vmCache.Values)
            {
                if (!hostCounts.ContainsKey(vm.Host))
                    hostCounts.Add(vm.Host, new HostVmCount { Name = vm.Host });
                hostCounts[vm.Host].Count += 1;
            }

            string hostname = hostCounts.Values
                .OrderBy(h => h.Count)
                .Select(h => h.Name)
                .FirstOrDefault();

            // string hostname = _vmCache.Values
            //     .GroupBy(o=>o.Host)
            //     .Select(g=> new { Host = g.Key, Count = g.Count()})
            //     .OrderBy(o=>o.Count).Select(o=>o.Host)
            //     .FirstOrDefault();

            if (hostname.HasValue() && _hostCache.ContainsKey(hostname))
                return _hostCache[hostname];
            else
                return FindHostByRandom();
        }

        private VimClient FindHostByRandom()
        {
            int i = new Random().Next(0, _hostCache.Values.Count() - 1);
            return _hostCache.Values.ElementAt(i);
        }

        private void InitHost(string host)
        {
            List<string> hosts = new List<string>();
            string match = new Regex(@"\[[\d-,]*\]").Match(host).Value;
            if (match.HasValue())
            {
                foreach(int i in match.ExpandRange())
                    hosts.Add(host.Replace(match, i.ToString()));
            }
            else
            {
                hosts.Add(host);
            }

            Parallel.ForEach(
                hosts,
                async (url) => {
                    try {
                        await AddHost(url);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to construct {0}", url);
                    }
                }
            );
        }

        private async Task AddHost(string url)
        {
            string hostname = new Uri(url).Host;
            _logger.LogDebug($"Adding host {hostname}");

            if (_hostCache.ContainsKey(hostname))
            {
                await _hostCache[hostname].Disconnect();
                _hostCache.TryRemove(hostname, out VimClient discard);
                await Task.Delay(100);
            }

            HypervisorServiceConfiguration hostOptions = _options.Clone<HypervisorServiceConfiguration>();
            if (!url.EndsWith("/sdk")) url += "/sdk";

            hostOptions.Url = url;
            hostOptions.Host = hostname;
            var vHost = new VimClient(
                hostOptions,
                _vmCache,
                _vlanman,
                _mill.CreateLogger<VimClient>()
            );
            _hostCache.AddOrUpdate(hostname, vHost, (k, v) => (v = vHost));
            _logger.LogDebug($"Added host {hostname}; cache: {_hostCache.Values.Count}");

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
    }

}
