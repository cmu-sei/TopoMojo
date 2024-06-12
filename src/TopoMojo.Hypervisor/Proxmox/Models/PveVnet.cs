namespace TopoMojo.Hypervisor.Proxmox.Models
{
    public class PveVnet
    {
        public int Tag { get; set; }
        public string Type { get; set; }
        public string Vnet { get; set; }
        public string Zone { get; set; }
        public string Alias { get; set; }
    }
}