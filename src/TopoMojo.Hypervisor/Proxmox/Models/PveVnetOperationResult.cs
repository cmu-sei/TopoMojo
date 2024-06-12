namespace TopoMojo.Hypervisor.Proxmox.Models
{
    public sealed class PveVnetOperationResult
    {
        public string NetName { get; set; }
        public PveVnet Vnet { get; set; }
        public PveVnetOperationType Type { get; set; } = PveVnetOperationType.Create;

        public override string ToString()
        {
            return $"{NetName} :: {Vnet.Zone}.{Vnet.Alias} :: {Type}";
        }
    }
}
