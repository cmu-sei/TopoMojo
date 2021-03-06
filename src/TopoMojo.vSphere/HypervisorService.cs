// Copyright 2020 Carnegie Mellon University. All Rights Reserved.
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
using TopoMojo.Abstractions;
using TopoMojo.Models;

namespace TopoMojo.vSphere
{
    public class HypervisorService : IHypervisorService, IHostedService
    {
        public HypervisorService(
            HypervisorServiceConfiguration options,
            ILoggerFactory mill
        )
        {
            _options = options;
            _mill = mill;
            _logger = _mill.CreateLogger<HypervisorService>();
            _hostCache = new ConcurrentDictionary<string, VimClient>();
            _affinityMap = new Dictionary<string, VimClient>();
            _vmCache = new ConcurrentDictionary<string, Vm>();
            _vlanman = new VlanManager(_options.Vlan);

            NormalizeOptions(_options);
        }

        private readonly HypervisorServiceConfiguration _options;
        private readonly VlanManager _vlanman;

        private readonly ILogger<HypervisorService> _logger;
        private readonly ILoggerFactory _mill;
        private ConcurrentDictionary<string, VimClient> _hostCache;
        private DateTime _lastCacheUpdate = DateTime.MinValue;
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
                int progress = await VerifyDisks(template);
                if (progress == 100)
                    vm.Status = "initialized";
                else
                    if (progress >= 0)
                    {
                        vm.Task = new VmTask { Name = "initializing", Progress = progress };
                    }
            }

            //include task
            return vm;
        }
        public async Task<Vm> Deploy(VmTemplate template)
        {

            var vm = await Load(template.Name + "#" + template.IsolationTag);
            if (vm != null)
                return vm;

            VimClient host = FindHostByAffinity(template.IsolationTag);
            _logger.LogDebug("deploy: host " + host.Name);

            NormalizeTemplate(template, host.Options);
            _logger.LogDebug("deploy: normalized "+ template.Name);

            if (!template.Disks.IsEmpty())
            {
                bool found = await host.FileExists(template.Disks[0].Path);
                if (!found)
                    throw new Exception("Template disks have not been prepared.");
            }

            if (!host.Options.Uplink.StartsWith("nsx."))
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
            VimClient host = FindHostByVm(id);
            return await host.Start(id);
        }

        public async Task<Vm> Stop(string id)
        {
            _logger.LogDebug("stopping " + id);
            VimClient host = FindHostByVm(id);
            return await host.Stop(id);
        }

        public async Task<Vm> Save(string id)
        {
            _logger.LogDebug("saving " + id);
            VimClient host = FindHostByVm(id);
            return await host.Save(id);
        }

        public async Task<Vm> Revert(string id)
        {
            _logger.LogDebug("reverting " + id);
            VimClient host = FindHostByVm(id);
            return await host.Revert(id);
        }

        public async Task<Vm> Delete(string id)
        {
            _logger.LogDebug("deleting " + id);
            VimClient host = FindHostByVm(id);
            Vm vm =  await host.Delete(id);
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
                VimClient host = FindHostByVm(vm.Id);
                tasks.Add(host.Delete(vm.Id));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks.ToArray());
            }
        }

        public async Task<Vm> ChangeConfiguration(string id, VmKeyValue change)
        {
            _logger.LogDebug("changing " + id + " " + change.Key + "=" + change.Value);

            Vm vm = await Load(id);

            if (vm == null)
                throw new InvalidOperationException();

            VimClient host = FindHostByVm(id);
            VmOptions vmo = null;

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
                isopath.Merge(host.Options.IsoStore);
                change = new VmKeyValue
                {
                    Key = "iso",
                    Value = isopath.ToString() + tag
                };
            }

            if (change.Key == "net" && !change.Value.StartsWith("_none_"))
            {
                vmo = await GetVmNetOptions(vm.Name.Tag());
                if (!vmo.Net.Contains(val))
                    throw new InvalidOperationException();
            }

            return await host.Change(id, change);
        }

        public async Task<Vm[]> Find(string term)
        {
            await Task.Delay(0);

            IEnumerable<Vm> q = _vmCache.Values;

            if (term.HasValue())
                q =  q.Where(o=>o.Id.Contains(term) || o.Name.Contains(term));

            return CheckProgress(q.ToArray());
        }

        public async Task<int> VerifyDisks(VmTemplate template)
        {
            if (template.Disks.Length == 0)
                return 100; //show good if no disks to verify

            foreach (VimClient vhost in _hostCache.Values)
            {
                int progress = await vhost.TaskProgress(template.Id);
                if (progress >= 0)
                    return progress;
            }

            // string pattern = @"blank-(\d+)([^\.]+)";
            // Match match = Regex.Match(template.Disks[0].Path, pattern);
            // if (match.Success)
            // {
            //     return 100; //show blank disk as created
            // }

            VimClient host = FindHostByRandom();
            NormalizeTemplate(template, host.Options);
            // if (template.Disks.Length > 0)
            // {
                _logger.LogDebug(template.Source + " " + template.Disks[0].Path);
                if (await host.FileExists(template.Disks[0].Path))
                {
                    return 100;
                }
            // }
            return -1;
        }

        public async Task<int> CreateDisks(VmTemplate template)
        {
            if (template.Disks.Length == 0)
                return -1;

            int progress = await VerifyDisks(template);
            if (progress < 0)
            {
                VimClient host = FindHostByRandom();
                if (template.Disks[0].Source.HasValue()) {
                    Task cloneTask = host.CloneDisk(template.Id, template.Disks[0].Source, template.Disks[0].Path);
                    progress = 0;
                } else {
                    await host.CreateDisk(template.Disks[0]);
                    progress = 100;
                }
            }
            return progress;
        }

        public async Task<int> DeleteDisks(VmTemplate template)
        {
            if (template.Disks.Length == 0)
                return -1;

            int progress = await VerifyDisks(template);
            if (progress < 0)
                return -1;

            if (progress == 100)
            {
                VimClient host = FindHostByRandom();
                foreach (VmDisk disk in template.Disks)
                {
                    //protect stock disks; only delete a disk if it is local to the workspace
                    //i.e. the disk folder matches the workspaceId
                    if (template.IsolationTag.HasValue() && disk.Path.Contains(template.IsolationTag))
                    {
                        Task deleteTask = host.DeleteDisk(disk.Path);
                    }
                }
                return -1;
            }
            throw new Exception("Cannot delete disk that isn't fully created.");
        }

        public async Task<ConsoleSummary> Display(string id)
        {
            ConsoleSummary info = null;

            Vm vm = await Load(id);

            if (vm != null)
            {
                VimClient host = _hostCache[vm.Host];
                string ticket = "";

                try
                {
                    ticket = await host.GetTicket(vm.Id);
                }
                catch  {}

                info = new ConsoleSummary
                {
                    Id = vm.Id,
                    Name = vm.Name.Untagged(),
                    IsolationId = vm.Name.Tag(),
                    Url = ticket,
                    IsRunning = vm.State == VmPowerState.Running
                };
            }

            return info ?? new ConsoleSummary();
        }

        public async Task<Vm> Answer(string id, VmAnswer answer)
        {
            VimClient host = FindHostByVm(id);
            return await host.AnswerVmQuestion(id, answer.QuestionId, answer.ChoiceKey);
        }

        public async Task<VmOptions> GetVmIsoOptions(string id)
        {
            VimClient host = FindHostByRandom();

            List<string> isos = new List<string>();

            string publicFolder = Guid.Empty.ToString();

            isos.AddRange(
                (await host.GetFiles(host.Options.IsoStore + id + "/*.iso", false))
            );
            isos.AddRange(
                (await host.GetFiles(host.Options.IsoStore + publicFolder + "/*.iso", false))
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

        private void NormalizeTemplate(VmTemplate template, HypervisorServiceConfiguration option)
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
                    //don't add tag if referencing a global vlan
                    if (!_vlanman.Contains(eth.Net))
                    {
                        eth.Net = rgx.Replace(eth.Net, "") + tag;
                    }
                }
            }
        }

        private VimClient FindHostByVm(string id)
        {
            return _hostCache[_vmCache[id].Host];
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
