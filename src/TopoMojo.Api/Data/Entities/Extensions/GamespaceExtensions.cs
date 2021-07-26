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
            // so strip that to find template.
            int x = name.LastIndexOf('_');

            // check full name
            var tmpl = gamespace.Workspace.Templates
                .Where(t => !t.IsHidden && t.Name == name)
                .FirstOrDefault();

            // check without replica index
            if (tmpl == null && x == name.Length - 2)
            {
                name = name.Substring(0, x);

                tmpl = gamespace.Workspace.Templates
                .Where(t => !t.IsHidden && t.Name == name)
                .FirstOrDefault();
            }

            return (!tmpl?.IsHidden) ?? false;
        }

    }
}
