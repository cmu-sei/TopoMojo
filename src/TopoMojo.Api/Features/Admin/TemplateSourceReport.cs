// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Controllers;

public class TemplateSourceReport
{
    /// <summary>
    /// Template Detail field the names go in: "disks" (vSphere disk paths) or "template" (Proxmox template vms).
    /// </summary>
    public string Field { get; set; }
    public List<Entry> Sources { get; set; } = [];

    public class Entry
    {
        public string Name { get; set; }
        public bool IsGlobal { get; set; }
        public string WorkspaceId { get; set; }
        /// <summary>
        /// Owning workspace name, or "(deleted)" when the owning workspace or template is gone.
        /// </summary>
        public string WorkspaceName { get; set; }
        public string TemplateName { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? Modified { get; set; }
    }

    public record TemplateRef(string Name, string WorkspaceId);

    /// <summary>
    /// Ids as stored: 32 hex digits. Null for anything that isn't a guid.
    /// </summary>
    public static string NormalizeId(string id)
        => Guid.TryParse(id, out Guid g) ? g.ToString("n") : null;

    /// <summary>
    /// Files each source under its workspace. A template id outranks a folder, since a template
    /// knows its workspace. Sources that name no workspace (base templates, shared folders) are global.
    /// </summary>
    public static TemplateSourceReport Build(
        TemplateSourceOptions options,
        IReadOnlyDictionary<string, TemplateRef> templates,
        IReadOnlyDictionary<string, string> workspaces)
    {
        string globalId = Guid.Empty.ToString("n");
        var report = new TemplateSourceReport { Field = options.Field };

        foreach (var source in options.Sources ?? [])
        {
            var entry = new Entry
            {
                Name = source.Name,
                Created = source.Created,
                Modified = source.Modified
            };

            string templateId = NormalizeId(source.TemplateId);
            string workspaceId = NormalizeId(source.WorkspaceId);

            if (templateId is not null && templates.TryGetValue(templateId, out TemplateRef template))
            {
                entry.TemplateName = template.Name;
                workspaceId = NormalizeId(template.WorkspaceId);
            }
            else if (templateId is not null && workspaceId is null)
            {
                // a copy whose template was deleted
                entry.WorkspaceName = "(deleted)";
                report.Sources.Add(entry);
                continue;
            }

            if (workspaceId is null || workspaceId == globalId)
            {
                entry.IsGlobal = true;
            }
            else
            {
                entry.WorkspaceId = workspaceId;
                entry.WorkspaceName = workspaces.TryGetValue(workspaceId, out string name)
                    ? name
                    : "(deleted)";
            }

            report.Sources.Add(entry);
        }

        return report;
    }
}
