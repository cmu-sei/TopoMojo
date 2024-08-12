namespace TopoMojo.Hypervisor.Proxmox.Models
{
    public class PveIso
    {
        public string Volid { get; set; }
        public string Format { get; set; }
        public string Content { get; set; }
        public int Ctime { get; set; }
        public int Size { get; set; }

        public string Name
        {
            get
            {
                return this.Volid.Split('/')[1];
            }
        }

        public string DisplayName
        {
            get
            {
                return this.Name.Replace('#', '/');
            }
        }
    }
}