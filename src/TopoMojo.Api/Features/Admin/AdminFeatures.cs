// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Controllers;

/// <summary>
/// Optional admin features an installation turns on in its settings.
/// </summary>
public class AdminFeatures
{
    /// <summary>
    /// Core:EnableTemplateSources: the Disks tab and api/admin/template-sources.
    /// </summary>
    public bool TemplateSources { get; set; }
}
