// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using VimClient;

namespace TopoMojo.Hypervisor.vSphere
{
    public static class VimExtensions
    {
        public static string AsString(this ManagedObjectReference mor)
        {
            return $"{mor.type}|{mor.Value}";
        }

        public static ManagedObjectReference AsReference(this string mor)
        {
            var a = mor.Split('|');
            return new ManagedObjectReference
            {
                type = a.First(),
                Value = a.Last()
            };
        }

        public static ManagedObjectReference AsVim(this Vm vm)
        {
            string[] mor = vm.Reference.Split('|');
            return new ManagedObjectReference { type = mor[0], Value = mor[1] };
        }

        public static void AddRam(this VirtualMachineConfigSpec vmcs, int ram)
        {
            vmcs.memoryMB = (ram > 0) ? ram * 1024 : 1024;
            vmcs.memoryMBSpecified = true;
        }

        public static void AddCpu(this VirtualMachineConfigSpec vmcs, string cpu)
        {
            string[] p = cpu.Split('x');
            if (!int.TryParse(p[0], out int sockets))
            {
                sockets = 1;
            }

            if (p.Length <= 1 || !int.TryParse(p[1], out int coresPerSocket))
            {
                coresPerSocket = 1;
            }

            vmcs.numCPUs = sockets * 1;
            vmcs.numCPUsSpecified = true;
            vmcs.numCoresPerSocket = coresPerSocket;
            vmcs.numCoresPerSocketSpecified = true;
        }

        public static void AddBootOption(this VirtualMachineConfigSpec vmcs, int delay)
        {
            if (delay != 0)
            {
                vmcs.bootOptions = new VirtualMachineBootOptions();
                if (delay > 0)
                {
                    vmcs.bootOptions.bootDelay = delay * 1000;
                    vmcs.bootOptions.bootDelaySpecified = true;
                }
                if (delay < 0)
                {
                    vmcs.bootOptions.enterBIOSSetup = true;
                    vmcs.bootOptions.enterBIOSSetupSpecified = true;
                }
            }
        }

        public static void AddGuestInfo(this VirtualMachineConfigSpec vmcs, string[] list)
        {
            List<OptionValue> options = [];
            foreach (string item in list)
            {
                OptionValue option = new();
                int x = item.IndexOf('=');
                if (x > 0)
                {
                    option.key = item[..x].Replace(" ", "").Trim();
                    if (!option.key.StartsWith("guestinfo."))
                        option.key = "guestinfo." + option.key;
                    option.value = item[(x + 1)..].Trim();
                    options.Add(option);
                }
            }
            vmcs.extraConfig = [.. options];
        }

        public static ObjectContent First(this ObjectContent[] tree, string type)
        {
            return tree.Where(o => o.obj.type.EndsWith(type)).FirstOrDefault();
        }

        public static ObjectContent FindTypeByName(this ObjectContent[] tree, string type, string name)
        {
            foreach (var content in tree.Where(o => o.obj.type.EndsWith(type)))
            {
                if (content.propSet
                    .Any(p => p.name == "name" && p.val.ToString().Equals(name, StringComparison.CurrentCultureIgnoreCase)))
                {
                    return content;
                }
            }
            return null;
        }

        public static ObjectContent[] FindType(this ObjectContent[] tree, string type)
        {
            return tree.Where(o => o.obj.type.EndsWith(type)).ToArray();
        }

        public static ObjectContent FindTypeByReference(this ObjectContent[] tree, ManagedObjectReference mor)
        {
            return tree
                .Where(o => o.obj.type == mor.type && o.obj.Value == mor.Value)
                .SingleOrDefault();
        }

        public static object GetProperty(this ObjectContent content, string name)
        {
            return content
                .propSet.Where(p => p.name == name)
                .Select(p => p.val)
                .SingleOrDefault();
        }

        public static bool IsInPool(this ObjectContent content, ManagedObjectReference pool)
        {
            return content.GetProperty("resourcePool") is ManagedObjectReference mor && mor.Value == pool.Value;
        }

        public static T Clone<T>(this T obj)
        {
            return JsonSerializer.Deserialize<T>(
                JsonSerializer.Serialize(obj)
            );
        }
    }
}
