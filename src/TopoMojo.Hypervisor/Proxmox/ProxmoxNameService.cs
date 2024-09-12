namespace TopoMojo.Hypervisor.Proxmox
{
    public interface IProxmoxNameService
    {
        string ToPveName(string name);
        string FromPveName(string pveName);
    }

    public class ProxmoxNameService : IProxmoxNameService
    {
        public bool IsPveName(string name)
            => name.Contains("--");

        public string ToPveName(string name)
            => name.Replace("#", "--");

        public string FromPveName(string pveName)
            => pveName.Replace("--", "#");
    }
}
