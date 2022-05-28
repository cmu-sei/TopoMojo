using System;
using System.Linq;

namespace TopoMojo.Api.Models
{
    public class Dispatch
    {
        public string Id { get; set; }
        public string ReferenceId { get; set; }
        public string Trigger { get; set; }
        public string TargetGroup { get; set; }
        public string TargetName { get; set; }
        public string Result { get; set; }
        public string Error { get; set; }
        public DateTimeOffset WhenUpdated { get; set; }
        public DateTimeOffset WhenCreated { get; set; }
    }

    public class NewDispatch
    {
        public string ReferenceId { get; set; }
        public string Trigger { get; set; }
        public string TargetGroup { get; set; }
        public string TargetName { get; set; }
    }

    public class ChangedDispatch
    {
        public string Id { get; set; }
        public string Result { get; set; }
        public string Error { get; set; }
        public string TargetName { get; set; }
        public DateTimeOffset WhenUpdated { get; set; }
    }

    public class DispatchSearch : Search
    {
        public string gs { get; set; }
        public string since { get; set; }

        public const string FilterPending = "pending";
        public bool WantsPending => Filter.Contains(FilterPending);

    }
}
