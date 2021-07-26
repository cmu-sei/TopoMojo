// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

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
        public static VmTemplate ToVirtualTemplate(this ConvergedTemplate template, string isolationTag = "")
        {
            TemplateUtility tu = new TemplateUtility(template.Detail);

            tu.Name = template.Name;

            tu.Networks = template.Networks;

            tu.Iso = template.Iso;

            tu.IsolationTag = isolationTag.NotEmpty()
                ? isolationTag
                : template.WorkspaceId ?? Guid.Empty.ToString()
            ;

            tu.Id = template.Id;

            tu.UseUplinkSwitch = template.WorkspaceUseUplinkSwitch;

            tu.AddGuestSettings(template.Guestinfo ?? "");

            return tu.AsTemplate();
        }

        public static VmTemplate SetHostAffinity(this VmTemplate template, bool requireHostAffinity)
        {
            template.HostAffinity = requireHostAffinity;

            if (requireHostAffinity)
                template.AutoStart = false;

            return template;
        }

        // public static void AddSettings(this ConvergedTemplate template, KeyValuePair<string,string>[] settings)
        // {
        //     TemplateUtility tu = new TemplateUtility(template.Detail);
        //     tu.GuestSettings = settings;
        //     template.Detail = tu.ToString();
        // }

        public static T Clone<T>(this T obj)
        {
            return JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(obj, null)
            );
        }
    }
}
