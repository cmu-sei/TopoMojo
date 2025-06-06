// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
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

namespace TopoMojo.Hypervisor.vMock
{
    public partial class MockHypervisorService : IHypervisorService, IHostedService
    {
        public MockHypervisorService(
            HypervisorServiceConfiguration podConfiguration,
            ILoggerFactory mill
        )
        {
            _optPod = podConfiguration;
            _mill = mill;
            _logger = _mill.CreateLogger<MockHypervisorService>();
            _vms = [];
            _tasks = [];
            _rand = new Random();
            NormalizeOptions(_optPod);
            _ = Task.Run(DeploymentHandler);
        }

        private readonly HypervisorServiceConfiguration _optPod;
        private readonly ILogger<MockHypervisorService> _logger;
        private readonly ILoggerFactory _mill;
        private readonly Random _rand;
        private readonly Dictionary<string, Vm> _vms;
        private readonly Dictionary<string, VmTask> _tasks;
        private readonly BlockingCollection<DeploymentContext> DeploymentCollection = [];

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
            string target = $"{template.Name}#{template.IsolationTag}";

            Vm vm = (await Find(target)).FirstOrDefault();

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
                    vm.Task = new VmTask
                    {
                        Name = "initializing",
                        Progress = progress.Sum() / progress.Length
                    };
                }
            }
            else
            {
                vm.Status = "deployed";
            }

            IncludeTask(target, vm);
            return vm;
        }

        private void IncludeTask(string key, Vm vm)
        {
            if (_tasks.TryGetValue(key, out VmTask value))
            {
                VmTask task = value;
                float elapsed = (int)DateTimeOffset.UtcNow.Subtract(task.WhenCreated).TotalSeconds;
                task.Progress = (int)Math.Min(100, elapsed / 10 * 100);
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

            if (_vms.TryGetValue(key, out Vm value))
            {
                _logger.LogDebug("vm {name} already deployed", vm.Name);
                vm = value;
                vm.Status = "deployed";
                return vm;
            }

            // if (
            //     template.Disks.Any() &&
            //     VerifyNormalizedDisks(template).Result.Any(i => i < 100)
            // ) throw new Exception("Disks have not been prepared.");

            await Delay();

            vm = new Vm
            {
                Id = Guid.NewGuid().ToString(),
                Name = template.Name,
                Path = "[mock] pod/vm",
                Status = "deployed"
            };

            _logger.LogDebug("deployed vm {name}", vm.Name);
            _vms.Add(vm.Id, vm);
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
            if (vm is not null && test == 0)
            {
                vm.Question = new VmQuestion
                {
                    Id = Guid.NewGuid().ToString(),
                    Prompt = "This vm has a question you must answer. Would you like to answer it?",
                    DefaultChoice = "yes",

                    Choices = [
                        new VmQuestionChoice { Key = "yes", Label = "Yes" },
                        new VmQuestionChoice { Key = "no", Label = "No" }
                    ]
                };
            }
            await Delay();
            return vm;
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
                    _ = await Stop(op.Id);
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
            vm.Task = new VmTask { Id = id, Name = "saving", WhenCreated = DateTimeOffset.UtcNow };
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

        public async Task<Vm> ChangeConfiguration(string id, VmKeyValue change, bool sudo = false)
        {
            Vm vm = TryFind(id);
            await Delay();
            return vm;
        }

        public async Task<Vm[]> Find(string term)
        {
            await Task.Delay(0);
            return term.HasValue()
            ? _vms.Values.Where(o => o.Id.Contains(term) || o.Name.Contains(term)).ToArray()
            : [.. _vms.Values];
        }

        readonly List<MockDisk> _disks = [];
        public async Task<int> CreateDisks(VmTemplate template)
        {
            int[] progress = await VerifyDisks(template);

            if (progress.Length == 0)
                return 100;

            int index = 0;
            foreach (var disk in template.Disks)
            {
                if (progress[index] >= 0)
                    continue;

                _disks.Add(new MockDisk
                {
                    Path = disk.Path,
                    CreatedAt = DateTimeOffset.UtcNow,
                    Disk = new VmDisk()
                });

                progress[index] = 100;
                index += 1;
            }

            return 0;
        }

        public async Task<int[]> VerifyDisks(VmTemplate template)
        {
            NormalizeTemplate(template, _optPod);
            return await VerifyNormalizedDisks(template);
        }
        public async Task<int[]> VerifyNormalizedDisks(VmTemplate template)
        {
            await Delay();

            int[] test = [];
            Console.WriteLine(test.Sum());

            var result = new int[template.Disks.Length];

            int index = 0;
            foreach (var disk in template.Disks)
            {
                if (disk.Path.Contains("blank-"))
                {
                    result[index] = 100;
                }
                else
                {
                    // check file existence
                    result[index] = _disks.Any(d => d.Path == disk.Path) ? 100 : -1;
                }

                index += 1;
            }

            return result;
        }


        public async Task DeleteDisks(VmTemplate template)
        {
            int[] progress = await VerifyDisks(template);
            int index = 0;
            foreach (var disk in template.Disks)
            {
                MockDisk mock = _disks.FirstOrDefault(o => o.Path == disk.Path);
                if (mock is null || progress[index] < 100)
                    continue;
                _logger.LogDebug("disk: deleting {path}", disk.Path);
                _disks.Remove(mock);
            }
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
            return _vms.TryGetValue(id, out Vm value)
                ? value
                : _vms.Values.Where(v => v.Name == id).FirstOrDefault()
            ;
        }
        public string Version
        {
            get
            {
                _logger.LogDebug("returning PodManager.Version");
                return "Generic Pod Manager, v17.02.13";
            }
        }

        private static void NormalizeTemplate(VmTemplate template, HypervisorServiceConfiguration option)
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
                template.Name = $"{template.Name.Untagged()}#{template.IsolationTag}";
            }
        }

        private async Task Delay()
        {
            int x = _rand.Next(500, 2500);
            Console.WriteLine($"delay: {x}");
            await Task.Delay(x);
        }

        public async Task<VmOptions> GetVmIsoOptions(string id)
        {
            await Task.Delay(0);
            VmOptions opt = new()
            {
                Iso = [
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
                ],
            };
            return opt;
        }
        public async Task<VmOptions> GetVmNetOptions(string id)
        {
            await Task.Delay(0);
            VmOptions opt = new()
            {
                Net = ["bridge-net", "isp-att", "lan#12345678"]
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

        private static void NormalizeOptions(HypervisorServiceConfiguration options)
        {
            var regex = DataStoreEndsWithRegex();

            if (!regex.IsMatch(options.VmStore))
                options.VmStore += "/";

            if (!regex.IsMatch(options.DiskStore))
                options.DiskStore += "/";

            if (!regex.IsMatch(options.IsoStore))
                options.IsoStore += "/";
        }

        private Task DeploymentHandler()
        {
            foreach (var ctx in DeploymentCollection.GetConsumingEnumerable())
                _ = DeployBatch(ctx);

            return Task.CompletedTask;
        }

        private async Task DeployBatch(DeploymentContext ctx)
        {
            _logger.LogInformation("Deploying Batch {id}", ctx.Id);
            var tasks = new List<Task<Vm>>();
            var existing = (await Find(ctx.Id)).Select(vm => vm.Name);
            var missing = ctx.Templates.Where(t => existing.Contains(t.Name).Equals(false));

            foreach (var template in missing)
                tasks.Add(Deploy(template, ctx.Privileged));

            await Task.WhenAll(tasks.ToArray());
        }

        public async Task Deploy(DeploymentContext ctx, bool wait = false)
        {
            if (wait)
                await DeployBatch(ctx);
            else
                DeploymentCollection.Add(ctx);
        }

        [GeneratedRegex("(]|/)$")]
        private static partial Regex DataStoreEndsWithRegex();
    }

    public class MockDisk
    {
        public DateTimeOffset CreatedAt { get; set; }
        public string Path { get; set; }
        public VmDisk Disk { get; set; }
    }
}
