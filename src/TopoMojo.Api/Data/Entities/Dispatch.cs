using System;
using System.ComponentModel.DataAnnotations;
using TopoMojo.Api.Data.Abstractions;

namespace TopoMojo.Api.Data
{
    public class Dispatch : IEntity
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
}
