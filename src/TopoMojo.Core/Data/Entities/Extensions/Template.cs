// Copyright 2020 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Data.Extensions
{
    public static class TemplateExtensions
    {
        public static bool IsLinked(this Template template)
        {
            return template.ParentId != null;
        }
    }
}
