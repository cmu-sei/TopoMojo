
namespace TopoMojo.Hypervisor
{

    public record DeploymentContext(
        string Id,
        bool Affinity,
        bool Privileged,
        VmTemplate[] Templates
    );
}
