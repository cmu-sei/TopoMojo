using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TopoMojo.Hypervisor.Meta;

public class MetaHypervisorService(IHypervisorService[] hypervisorServices) : IHypervisorService
{
    public string Version => "";

    public HypervisorServiceConfiguration Options => null;

    private async Task<IHypervisorService> GetService(string id)
    {
        var tasks = new Dictionary<IHypervisorService, Task<Vm>>();

        foreach (var hypervisorService in hypervisorServices)
        {
            tasks.Add(hypervisorService, hypervisorService.Load(id));
        }

        await Task.WhenAll(tasks.Select(x => x.Value));

        var task = tasks.Where(x => x.Value.Result is not null && x.Value.Result.Id is not null).SingleOrDefault();
        return task.Key;
    }

    private static string GetId(string id)
    {
        return id.Split('/').Last();
    }

    public Task<Vm> Answer(string id, VmAnswer answer)
    {
        throw new System.NotImplementedException();
    }

    public Task<Vm> ChangeConfiguration(string id, VmKeyValue change, bool privileged = false)
    {
        throw new System.NotImplementedException();
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
                _ = await Stop(op.Id);
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

    public Task<int> CreateDisks(VmTemplate template)
    {
        throw new System.NotImplementedException();
    }

    public Task<Vm> Delete(string id)
    {
        throw new System.NotImplementedException();
    }

    public Task DeleteAll(string target)
    {
        throw new System.NotImplementedException();
    }

    public Task DeleteDisks(VmTemplate template)
    {
        throw new System.NotImplementedException();
    }

    public Task<Vm> Deploy(VmTemplate template, bool privileged = false)
    {
        throw new System.NotImplementedException();
    }

    public Task Deploy(DeploymentContext ctx, bool wait = false)
    {
        throw new System.NotImplementedException();
    }

    public Task<VmConsole> Display(string id)
    {
        throw new System.NotImplementedException();
    }

    public Task<Vm[]> Find(string searchText)
    {
        throw new System.NotImplementedException();
    }

    public Task<VmOptions> GetVmIsoOptions(string key)
    {
        throw new System.NotImplementedException();
    }

    public Task<VmOptions> GetVmNetOptions(string key)
    {
        throw new System.NotImplementedException();
    }

    public Task<Vm> Load(string id)
    {
        throw new System.NotImplementedException();
    }

    public Task<Vm> Refresh(VmTemplate template)
    {
        throw new System.NotImplementedException();
    }

    public Task ReloadHost(string host)
    {
        throw new System.NotImplementedException();
    }

    public Task<Vm> Revert(string id)
    {
        throw new System.NotImplementedException();
    }

    public Task<Vm> Save(string id)
    {
        throw new System.NotImplementedException();
    }

    public Task SetAffinity(string isolationTag, Vm[] vms, bool start)
    {
        throw new System.NotImplementedException();
    }

    public async Task<Vm> Start(string id)
    {
        var service = await GetService(id);
        return await service?.Start(id);
    }

    public Task StartAll(string target)
    {
        throw new System.NotImplementedException();
    }

    public async Task<Vm> Stop(string id)
    {
        var service = await GetService(id);
        return await service?.Stop(id);
    }

    public Task StopAll(string target)
    {
        throw new System.NotImplementedException();
    }

    public Task<int[]> VerifyDisks(VmTemplate template)
    {
        throw new System.NotImplementedException();
    }
}