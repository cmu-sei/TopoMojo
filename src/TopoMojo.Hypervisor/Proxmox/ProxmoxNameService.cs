namespace TopoMojo.Hypervisor.Proxmox
{
    public interface IProxmoxNameService
    {
        string ToPveName(string name);
        string FromPveName(string pveName);
    }

    public class ProxmoxNameService : IProxmoxNameService
    {
        public string ToPveName(string name)
        {
            return name.Replace("#", "--");
        }

        public string FromPveName(string pveName)
        {
            return pveName.Replace("--", "#");
        }
    }
}
