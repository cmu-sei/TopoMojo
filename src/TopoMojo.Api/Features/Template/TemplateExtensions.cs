// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using System;
using System.Collections.Generic;
using System.Text.Json;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;

namespace TopoMojo
{
    public static class TemplateExtensions
    {
        /// <summary>
        /// True when ';' should be treated as a Guest Settings separator, which is the legacy
        /// vSphere behavior. Proxmox delivers ';' to the guest as ordinary data, so splitting on it
        /// would silently truncate values.
        /// </summary>
        public static bool AllowsSemicolonGuestSettingSeparator(this IHypervisorService pod)
            => AllowsSemicolonGuestSettingSeparator(pod?.Options?.HypervisorType);

        /// <inheritdoc cref="AllowsSemicolonGuestSettingSeparator(IHypervisorService)"/>
        public static bool AllowsSemicolonGuestSettingSeparator(HypervisorType? hypervisorType)
            => hypervisorType != HypervisorType.Proxmox;

        public static VmTemplate ToVirtualTemplate(this ConvergedTemplate template, string isolationTag = "", bool allowSemicolonSeparator = true)
        {
            TemplateUtility tu = new(template.Detail)
            {
                Name = template.Name,

                Networks = template.Networks,

                Iso = template.Iso,

                IsolationTag = isolationTag.NotEmpty()
                    ? isolationTag
                    : template.WorkspaceId ?? Guid.Empty.ToString()
                ,

                Id = template.Id,

                UseUplinkSwitch = template.WorkspaceUseUplinkSwitch
            };

            tu.AddGuestSettings(template.Guestinfo ?? "", allowSemicolonSeparator);

            return tu.AsTemplate();
        }

        public static VmTemplate SetHostAffinity(this VmTemplate template, bool requireHostAffinity)
        {
            template.HostAffinity = requireHostAffinity;

            if (requireHostAffinity)
                template.AutoStart = false;

            return template;
        }

        public static T Clone<T>(this T obj)
        {
            return JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(obj)
            );
        }
    }
}
