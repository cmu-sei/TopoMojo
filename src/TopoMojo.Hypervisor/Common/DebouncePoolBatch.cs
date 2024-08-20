using System.Collections.Generic;

namespace TopoMojo.Hypervisor
{
    public sealed class DebouncePoolBatch<T>
    {
        public string Id { get; set; }
        public IEnumerable<T> Items { get; set; }
    }
}
