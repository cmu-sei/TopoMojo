// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.Json;
using System.Text.RegularExpressions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Services
{
    public partial class TemplateUtility
    {
        public TemplateUtility(string detail, string diskname = "placeholder")
        {
            if (detail.NotEmpty())
            {
                _template = JsonSerializer.Deserialize<VmTemplate>(detail, JsonDeserializerOption);
            }
            else
            {
                diskname = MyRegex().Replace(diskname, "-").Replace("--", "-").Trim('-').ToLower();

                _template = new VmTemplate
                {
                    Ram = 4,
                    VideoRam = 0,
                    Cpu = "1x2",
                    Adapters = 1,
                    Eth = [new VmNet { Net = "lan", Type="e1000" }],
                    Disks = [ new VmDisk
                    {
                        Path = $"[ds] {Guid.Empty}/{diskname}.vmdk",
                        Source = "",
                        Controller = "lsilogic",
                        Size = 10
                    }]
                };
            }
        }

        private VmTemplate _template = null;
        private JsonSerializerOptions JsonDeserializerOption => new()
        {
            PropertyNameCaseInsensitive = true
        };
        private JsonSerializerOptions JsonOptions => new()
        {
            WriteIndented = true,
        };

        public string Id
        {
            get { return _template.Id; }
            set { _template.Id = value; }
        }

        public string Name
        {
            get { return _template.Name; }
            set { _template.Name = value; }
        }

        public string IsolationTag
        {
            get { return _template.IsolationTag; }
            set { _template.IsolationTag = value; }
        }

        public string Networks
        {
            get { return string.Join(", ", _template.Eth.Select(e => e.Net)); }

            set
            {
                List<VmNet> nics = [.. _template.Eth];

                if (nics.Count == 0 || !value.NotEmpty())
                    return;

                VmNet proto = nics.First();

                string[] nets = value.Split(AppConstants.StringTokenSeparators, StringSplitOptions.RemoveEmptyEntries);

                if (nets.Length == 0) nets = ["lan"];

                for (int i = nics.Count; i > nets.Length; i--)
                    nics.RemoveAt(i-1);

                for (int i = 0; i < nets.Length; i++)
                {
                    if (nics.Count < i+1)
                    {
                        nics.Add(new VmNet{
                            Type = proto.Type,
                        });
                    }

                    nics[i].Net = nets[i];
                }

                _template.Eth = [.. nics];
            }
        }

        public string Iso
        {
            get { return _template.Iso; }
            set { _template.Iso = value; }
        }

        public bool UseUplinkSwitch
        {
            get { return _template.UseUplinkSwitch; }
            set { _template.UseUplinkSwitch = value; }
        }

        public VmKeyValue[] GuestSettings
        {
            get { return _template.GuestSettings; }
            set { _template.GuestSettings = value; }
        }

        public void AddGuestSettings(string guestinfo)
        {
            var result = new List<VmKeyValue>();

            if (_template.GuestSettings != null)
                result.AddRange(_template.GuestSettings);

            var lines = guestinfo?.Split(
                AppConstants.StringLineSeparators,
                StringSplitOptions.RemoveEmptyEntries
            ) ?? [];

            foreach (var line in lines)
            {
                int x = line.IndexOf('=');

                if (x > 0)
                {
                    string key = line[..x].Trim();

                    if (key.StartsWith(AppConstants.TagDelimiter))
                        continue;

                    if (!key.StartsWith("guestinfo."))
                        key = "guestinfo." + key;

                    result.Add(
                        new VmKeyValue
                        {
                            Key = key,
                            Value = line[(x + 1)..].Trim()
                        }
                    );
                }
            }

            _template.GuestSettings = [.. result];
        }

        public void LocalizeDiskPaths(string workspaceKey, string templateKey)
        {
            if (workspaceKey.IsEmpty() || templateKey.IsEmpty())
                throw new InvalidOperationException();

            string path = $"[ds] {workspaceKey}/{templateKey}";

            int i = 0;

            foreach (VmDisk disk in _template.Disks)
            {
                if (!disk.Path.StartsWith(path))
                {
                    disk.Source = disk.Path;

                    disk.Path = $"{path}_{i}.vmdk";
                }

                i += 1;
            }

            // Proxmox
            // TODO: Move to separate method?
            if (!string.IsNullOrEmpty(_template.Template))
            {
                _template.ParentTemplate = _template.Template;
                _template.Template = _template.Name;
            }
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(_template, JsonOptions);
        }

        public VmTemplate AsTemplate()
        {
            return _template;
        }

        [GeneratedRegex(@"[^\w\d]")]
        private static partial Regex MyRegex();
    }
}
