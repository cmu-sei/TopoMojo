// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Data.Extensions
{
    public static class GamespaceExtensions
    {
        public static bool IsTemplateVisible(this Gamespace gamespace, string name)
        {
            name = name.Untagged();

            // a vm could be a replica, denoted by `_1` or some number,
            // or a variant-vm, with `_v1` removed earlier
            // so check name, name-with-variant-suffix, name-without-replica-suffix, name-without-replica-suffix-with-variant-suffix

            // check full name
            var tmpl = gamespace.Workspace.Templates
                .Where(t => t.Name == name)
                .FirstOrDefault();

            // check with variant suffix
            if (tmpl == null)
            {
                tmpl = gamespace.Workspace.Templates
                    .Where(t => t.Name == $"{name}_v{gamespace.Variant + 1}")
                    .FirstOrDefault();
            }

            // check without replica suffix
            if (tmpl == null)
            {
                name = name.Substring(0, name.LastIndexOf('_'));

                tmpl = gamespace.Workspace.Templates
                .Where(t => t.Name == name)
                .FirstOrDefault();

                if (tmpl == null)
                {
                    tmpl = gamespace.Workspace.Templates
                        .Where(t => t.Name == $"{name}_v{gamespace.Variant + 1}")
                        .FirstOrDefault();
                }
            }

            return (!tmpl?.IsHidden) ?? false;
        }

    }
}
