// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;

namespace TopoMojo.Api.Data
{
    public class Worker
    {
        public string WorkspaceId { get; set; }
        public string SubjectId { get; set; }
        public string SubjectName { get; set; }
        public Permission Permission { get; set; }
        public virtual Workspace Workspace { get; set; }
        [NotMapped] public bool CanManage => Permission.HasFlag(Permission.Manager);
        [NotMapped] public bool CanEdit => Permission.HasFlag(Permission.Editor);
    }
}
