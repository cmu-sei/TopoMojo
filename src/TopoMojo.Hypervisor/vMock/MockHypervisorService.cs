// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor.vMock
{
    public class MockHypervisorService : IHypervisorService, IHostedService
    {
        public MockHypervisorService(
            HypervisorServiceConfiguration podConfiguration,
            ILoggerFactory mill
        )
        {
            _optPod = podConfiguration;
            _mill = mill;
            _logger = _mill.CreateLogger<MockHypervisorService>();
            _vms = new Dictionary<string, Vm>();
            _tasks = new Dictionary<string, VmTask>();
            _rand = new Random();

            NormalizeOptions(_optPod);
        }

        private readonly HypervisorServiceConfiguration _optPod;
        private readonly ILogger<MockHypervisorService> _logger;
        private readonly ILoggerFactory _mill;
        private Random _rand;
        private Dictionary<string, Vm> _vms;
        private Dictionary<string, VmTask> _tasks;

        public HypervisorServiceConfiguration Options { get { return _optPod; } }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public async Task<Vm> Refresh(VmTemplate template)
        {
            Vm vm = null;
            NormalizeTemplate(template, _optPod);
            string key = template.Name; //template.IsolationTag + "-" + template.Id;
            vm = (await Find(key)).FirstOrDefault();
            if (vm != null)
            {

                vm.Status = "deployed";
                //IncludeTask(key, vm);

            }
            else
            {
                vm = new Vm() { Name = template.Name, Status = "created" };
                if (VerifyDisks(template).Result == 100)
                    vm.Status = "initialized";
            }

            IncludeTask(key, vm);
            return vm;
        }

        private void IncludeTask(string key, Vm vm)
        {
            if (_tasks.ContainsKey(key))
            {
                VmTask task = _tasks[key];
                float elapsed = (int)DateTimeOffset.UtcNow.Subtract(task.WhenCreated).TotalSeconds;
                task.Progress = (int) Math.Min(100, (elapsed / 10) * 100);
                if (task.Progress == 100)
                {
                    _tasks.Remove(key);
                    task = null;
                }
                vm.Task = task;
            }
        }

        public async Task<Vm> Deploy(VmTemplate template, bool privileged = false)
        {
            NormalizeTemplate(template, _optPod);
            string key = template.Name;
            //string key = template.IsolationTag + "-" + template.Id;
            Vm vm = null;
            if (!_vms.ContainsKey(key))
            {
                if (template.Disks.Length > 0)
                {
                    if (template.Disks[0].Path.Contains("blank"))
                        throw new Exception("Disks have not been prepared");
                    if (VerifyDisks(template).Result != 100)
                        throw new Exception("Disks have not been prepared.");
                }
                await Delay();
                vm = new Vm
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = template.Name,
                    Path = "[mock] pod/vm",
                    Status = "deployed"
                };
                _logger.LogDebug($"deployed vm {vm.Name}");
                _vms.Add(vm.Id, vm);
            }
            else
            {
                vm = _vms[key];
                vm.Status = "deployed";
                _logger.LogDebug($"vm {vm.Name} already deployed");
            }
            return vm;
        }

        public async Task SetAffinity(string isolationTag, Vm[] vms, bool start)
        {
            await Task.Delay(0);
        }

        public async Task<Vm> Load(string id)
        {
            Vm vm = TryFind(id);
            int test = _rand.Next(9);
            if (test == 0)
            {
                vm.Question = new VmQuestion {
                    Id = Guid.NewGuid().ToString(),
                    Prompt = "This vm has a question you must answer. Would you like to answer it?",
                    DefaultChoice = "yes",

                    Choices = new VmQuestionChoice[] {
                        new VmQuestionChoice { Key="yes", Label="Yes" },
                        new VmQuestionChoice { Key="no", Label="No" }
                    }
                };
            }
            await Delay();
            return _vms[id];
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

                case VmOperationType.Reset:
                vm = await Stop(op.Id);
                vm = await Start(op.Id);
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
            Vm vm = TryFind(id);
            await Delay();
            vm.State = VmPowerState.Running;
            return vm;
        }

        public async Task<Vm> Stop(string id)
        {
            Vm vm = TryFind(id);
            await Delay();
            vm.State = VmPowerState.Off;
            return vm;
        }

        public async Task<Vm> Save(string id)
        {
            if (_tasks.ContainsKey(id))
                throw new InvalidOperationException();

            Vm vm = TryFind(id);
            vm.Task = new VmTask { Id = id, Name = "saving", WhenCreated = DateTimeOffset.UtcNow};
            _tasks.Add(vm.Name, vm.Task);
            await Delay();
            return vm;
        }

        public async Task<Vm> Revert(string id)
        {
            Vm vm = TryFind(id);
            await Delay();
            vm.State = VmPowerState.Off;
            return vm;
        }

        public async Task<Vm> Delete(string id)
        {
            Vm vm = TryFind(id);
            await Delay();
            _vms.Remove(id);
            vm.State = VmPowerState.Off;
            vm.Status = "initialized";
            return vm;
        }

        public async Task<Vm> ChangeConfiguration(string id, VmKeyValue change)
        {
            Vm vm = TryFind(id);
            await Delay();
            return vm;
        }

        public async Task<Vm[]> Find(string term)
        {
            await Task.Delay(0);
            return (term.HasValue())
            ?  _vms.Values.Where(o=> o.Id.Contains(term) || o.Name.Contains(term)).ToArray()
            : _vms.Values.ToArray();
        }

        List<MockDisk> _disks = new List<MockDisk>();
        public async Task<int> CreateDisks(VmTemplate template)
        {
            NormalizeTemplate(template, _optPod);
            string key = template.Name; //template.IsolationTag + "-" + template.Id;
            Vm vm = (await Find(key)).FirstOrDefault();
            if (vm != null)
                return 100;


            int progress = await VerifyDisks(template);
            if (progress < 0)
            {
                VmDisk disk = template.Disks.First();
                // if (!_tasks.ContainsKey(key))
                //     _tasks.Add(key, new VmTask {
                //         Name = "initializing",
                //         WhenCreated = DateTimeOffset.UtcNow,
                //         Id = key
                //     });
                _logger.LogDebug("disk: creating " + disk.Path);
                _disks.Add(new MockDisk
                {
                    CreatedAt = DateTimeOffset.Now,
                    Path = disk.Path,
                    Disk = disk
                });
            }
            return progress;
        }

        public async Task<int> CreateDisksOld(VmTemplate template)
        {
            int progress = await VerifyDisks(template);
            if (progress < 0)
            {
                VmDisk disk = template.Disks.First();
                _logger.LogDebug("disk: creating " + disk.Path);
                _disks.Add(new MockDisk
                {
                    CreatedAt = DateTimeOffset.Now,
                    Path = disk.Path,
                    Disk = disk
                });
            }
            return 0;
        }

        public async Task<int> VerifyDisks(VmTemplate template)
        {
            await Delay();

            NormalizeTemplate(template, _optPod);
            int progress = -1;
            VmDisk disk = template.Disks.FirstOrDefault();
            if (disk != null)
            {

                if (disk.Path.Contains("blank-"))
                    return 100;

                MockDisk mock = _disks.FirstOrDefault(o=>o.Path == disk.Path);
                if (mock == null)
                {    _disks.Add(new MockDisk
                    {
                        CreatedAt = DateTimeOffset.Now,
                        Path = disk.Path,
                        Disk = disk
                    });
                }
                progress = 100;
                // if (mock != null)
                // {
                //     float elapsed = (int)DateTimeOffset.Now.Subtract(mock.CreatedAt).TotalSeconds;
                //     progress = (int) Math.Min(100, (elapsed / 10) * 100);
                // }
            }
            return progress;
        }


        public async Task<int> DeleteDisks(VmTemplate template)
        {
            int progress = await VerifyDisks(template);
            if (progress < 0)
                return -1;

            if (progress == 100)
            {
                VmDisk disk = template.Disks.First();
                MockDisk mock = _disks.FirstOrDefault(o=>o.Path == disk.Path);
                if (mock != null)
                {
                    _logger.LogDebug("disk: deleting " + disk.Path);
                    _disks.Remove(mock);
                    return -1;
                }
            }
            throw new Exception("Cannot delete disk that isn't fully created.");
        }

        public async Task<VmConsole> Display(string id)
        {
            await Task.Delay(0);
            var vm = TryFind(id);
            return vm is null
                ? new VmConsole()
                : new VmConsole
                    {
                        Id = vm.Id,
                        Name = vm.Name.Untagged(),
                        IsolationId = vm.Name.Tag(),
                        Url = "https://mock.topomojo.local/ticket/12345678",
                        IsRunning = vm.State == VmPowerState.Running
                    }
            ;
        }

        private Vm TryFind(string id)
        {
            return _vms.ContainsKey(id)
                ? _vms[id]
                : _vms.Values.Where(v => v.Name == id).FirstOrDefault();
        }
        public string Version
        {
            get
            {
                _logger.LogDebug("returning PodManager.Version");
                return "Generic Pod Manager, v17.02.13";
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
                    // //don't add tag if referencing a global vlan
                    // if (!_vlanman.Contains(eth.Net))
                    // {
                    //     eth.Net = rgx.Replace(eth.Net, "") + tag;
                    // }
                }
            }
        }

        private void NormalizeTemplateOld(VmTemplate template, HypervisorServiceConfiguration option)
        {
            if (template.Iso.HasValue() && !template.Iso.StartsWith(option.IsoStore))
            {
                template.Iso = option.IsoStore + template.Iso + ".iso";
            }

            // if (template.Source.HasValue() && !template.Source.StartsWith(option.StockStore))
            // {
            //     template.Source = option.StockStore + template.Source + ".vmdk";
            // }

            foreach (VmDisk disk in template.Disks)
            {
                if (!disk.Path.StartsWith(option.DiskStore))
                    disk.Path = option.DiskStore + disk.Path + ".vmdk";
            }

            if (template.IsolationTag.HasValue())
            {
                string tag = "#" + template.IsolationTag;
                Regex rgx = new Regex("#.*");
                if (!template.Name.EndsWith(template.IsolationTag))
                    template.Name = rgx.Replace(template.Name, "") + tag;
                foreach (VmNet eth in template.Eth)
                    eth.Net = rgx.Replace(eth.Net, "") + tag;
            }
        }

        private async Task Delay()
        {
            int x = _rand.Next(500,2500);
            Console.WriteLine($"delay: {x}");
            await Task.Delay(x);
        }

        public async Task<VmOptions> GetVmIsoOptions(string id)
        {
            await Task.Delay(0);
            VmOptions opt = new VmOptions()
            {
                Iso = new string[] {
                    "test1.iso",
                    "test2.iso",
                    "test3.iso",
                    "test4.iso",
                    "test5.iso",
                    "test6.iso",
                    "test7.iso",
                    "test8.iso",
                    "test9.iso",
                    "test10.iso",
                    "test11.iso",
                    "test12.iso",
                    "test13.iso",
                    "test14.iso",
                    "test15.iso",
                    "test16.iso",
                    "test17.iso",
                    "test18.iso",
                    "really-long-iso-name-that-needs-to-wrap-1.0.0.test2.iso"
                },
            };
            return opt;
        }
        public async Task<VmOptions> GetVmNetOptions(string id)
        {
            await Task.Delay(0);
            VmOptions opt = new VmOptions()
            {
                Net = new string[] { "bridge-net", "isp-att" }
            };
            return opt;
        }

        public async Task<Vm> Answer(string id, VmAnswer answer)
        {
            await Task.Delay(0);
            Vm vm = TryFind(id);
            vm.Question = null;
            return vm;
        }

        public async Task ReloadHost(string host)
        {
            await Task.Delay(0);
        }

        public async Task DeleteAll(string target)
        {
            foreach (var vm in _vms.Values.Where(v => v.Name.Contains(target)).ToArray())
            {
                await Task.Delay(50);
                _vms.Remove(vm.Id);
            }
        }

        public Task StartAll(string target)
        {
            throw new NotImplementedException();
        }

        public Task StopAll(string target)
        {
            throw new NotImplementedException();
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

    public class MockDisk
    {
        public DateTimeOffset CreatedAt { get; set; }
        public string Path { get; set; }
        public VmDisk Disk { get; set; }
    }
}
