// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Linq;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Models
{
    public class Template
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Audience { get; set; }
        public string Networks { get; set; }
        public string Iso { get; set; }
        public string Guestinfo { get; set; }
        public bool IsHidden { get; set; }
        public string ParentId { get; set; }
        public int Replicas { get; set; }
        public string WorkspaceId { get; set; }
    }

    public class ChangedTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Networks { get; set; }
        public string Iso { get; set; }
        public bool IsHidden { get; set; }
        public string Guestinfo { get; set; }
        public int Replicas { get; set; }
    }

    public class NewTemplateDetail: TemplateDetail
    {
    }

    public class TemplateDetail
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Audience { get; set; }
        public string Networks { get; set; }
        public string Guestinfo { get; set; }
        public string Detail { get; set; }
        public bool IsPublished { get; set; }
    }

    public class TemplateSummary
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Audience { get; set; }
        public string WorkspaceId { get; set; }
        public string WorkspaceName { get; set; }
        public string ParentId { get; set; }
        public string ParentName { get; set; }
        public bool IsPublished { get; set; }
    }

    public class TemplateLink
    {
        public string TemplateId { get; set; }
        public string WorkspaceId { get; set; }
    }

    public class ConvergedTemplate
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Networks { get; set; }
        public string Iso { get; set; }
        public string Detail { get; set; }
        public string Guestinfo { get; set; }
        public string WorkspaceId { get; set; }
        public bool WorkspaceUseUplinkSwitch { get; set; }
        public int Replicas { get; set; }
    }

    public class TemplateSearch: Search
    {
        public const string PublishFilter = "published";
        public const string ParentFilter = "parents";
        public bool WantsAudience => aud.NotEmpty();
        public bool WantsPublished => Filter.Contains(PublishFilter);
        public bool WantsParents => Filter.Contains(ParentFilter);

        public string aud { get; set; }
        public string pid { get; set; }
    }
}
