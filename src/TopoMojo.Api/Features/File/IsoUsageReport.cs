// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Controllers;

public class IsoUsageReport
{
    public List<TemplateReference> Templates { get; set; } = [];
    public List<GamespaceReference> ActiveGamespaces { get; set; } = [];

    public class TemplateReference
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string WorkspaceName { get; set; }
    }

    public class GamespaceReference
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}
