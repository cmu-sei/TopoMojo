// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Threading.Tasks;

namespace TopoMojo.Hypervisor
{
    public interface IHypervisorService
    {
        Task<Vm> Load(string id);
        Task<Vm> Start(string id);
        Task<Vm> Stop(string id);
        Task<Vm> Save(string id);
        Task<Vm> Revert(string id);
        Task<Vm> Delete(string id);
        Task StartAll(string target);
        Task StopAll(string target);
        Task DeleteAll(string target);
        Task<Vm> ChangeState(VmOperation op);
        Task<Vm> ChangeConfiguration(string id, VmKeyValue change, bool privileged = false);
        Task<Vm> Deploy(VmTemplate template, bool privileged = false);
        Task Deploy(DeploymentContext ctx, bool wait = false);
        Task SetAffinity(string isolationTag, Vm[] vms, bool start);
        Task<Vm> Refresh(VmTemplate template);
        Task<Vm[]> Find(string searchText);
        Task<int> CreateDisks(VmTemplate template);
        Task<int[]> VerifyDisks(VmTemplate template);
        Task DeleteDisks(VmTemplate template);
        Task<VmConsole> Display(string id);
        Task<Vm> Answer(string id, VmAnswer answer);
        // Task<TemplateOptions> GetTemplateOptions(string key);
        Task<VmOptions> GetVmIsoOptions(string key);
        Task<VmOptions> GetVmNetOptions(string key);
        string Version { get; }
        Task ReloadHost(string host);
        HypervisorServiceConfiguration Options { get; }
    }

}
