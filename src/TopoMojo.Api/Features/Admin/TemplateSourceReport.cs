// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Controllers;

public class TemplateSourceReport
{
    /// <summary>
    /// False when Core:EnableTemplateSources is off; the hypervisor isn't asked and Sources is empty.
    /// </summary>
    public bool Enabled { get; set; } = true;

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
        /// Owning workspace name, or "(deleted)" when the owning workspace is gone, or when the
        /// source names only a template and that template is gone.
        /// </summary>
        public string WorkspaceName { get; set; }
        /// <summary>
        /// Template the source was copied for, or "(deleted)" when that template is gone.
        /// </summary>
        public string TemplateName { get; set; }
        public DateTimeOffset? Created { get; set; }
        public DateTimeOffset? Modified { get; set; }
    }

    public record TemplateRef(string Name, string WorkspaceId);

    /// <summary>
    /// One key per id: a guid as 32 hex digits, whether it's stored that way or dashed (older
    /// versions and imports). A workspace id with a Core:Tenant prefix isn't a guid and is kept
    /// as it is. Null for anything else.
    /// </summary>
    public static string NormalizeId(string id)
    {
        if (Guid.TryParse(id, out Guid g))
            return g.ToString("n");

        return IsTenantId(id) ? id : null;
    }

    /// <summary>
    /// A guid, plain or dashed, whose first characters are replaced by a tenant's letters (g-z),
    /// as WorkspaceService makes ids and TransferService re-prefixes imported ones.
    /// </summary>
    private static bool IsTenantId(string id)
    {
        if (id is null || (id.Length != 32 && id.Length != 36))
            return false;

        string tenant = TenantOf(id);

        if (tenant.Length == 0 || tenant.Length >= id.Length)
            return false;

        // put back the dashes: a long tenant writes over them, and a later import under a shorter
        // tenant pads that stretch with zeros, then check what's left is a guid
        char[] guid = [.. new string('0', tenant.Length), .. id.AsSpan(tenant.Length)];
        if (guid.Length == 36)
            foreach (int i in (int[])[8, 13, 18, 23])
                guid[i] = '-';

        return Guid.TryParse(guid, out _);
    }

    /// <summary>
    /// The forms a normalized id can be stored in: a guid as 32 hex digits or dashed, a tenant id as it is.
    /// </summary>
    public static IEnumerable<string> StoredForms(IEnumerable<string> normalizedIds)
        => normalizedIds.SelectMany(id => Guid.TryParse(id, out Guid g)
            ? new[] { g.ToString("n"), g.ToString("d") }
            : id is not null ? [id] : []).Distinct();

    /// <summary>
    /// The Core:Tenant an id was made with: its leading letters g-z (the letters that aren't hex
    /// digits), or "" for none.
    /// </summary>
    public static string TenantOf(string id)
        => new([.. id.ToLowerInvariant().TakeWhile(c => c >= 'g' && c <= 'z')]);


    /// <summary>
    /// Files each source under its workspace. A template id outranks a folder, since a template
    /// knows its workspace. Sources that name no workspace (base templates, shared folders) are global.
    /// </summary>
    /// <remarks>
    /// A workspace that isn't in this database and whose id carries another Core:Tenant prefix
    /// belongs to another TopoMojo on the same hypervisor, which can delete its disks at any time,
    /// so its sources are left out. A missing workspace in this tenant is shown as "(deleted)".
    /// Proxmox copies are named by template id, which has no tenant, so those can't be told apart.
    /// </remarks>
    public static TemplateSourceReport Build(
        TemplateSourceOptions options,
        IReadOnlyDictionary<string, TemplateRef> templates,
        IReadOnlyDictionary<string, string> workspaces,
        string tenant = "")
    {
        string globalId = Guid.Empty.ToString("n");
        var report = new TemplateSourceReport { Field = options.Field };

        bool IsOtherTenants(string workspaceId)
            => workspaceId is not null
            && workspaceId != globalId
            && !workspaces.ContainsKey(workspaceId)
            && TenantOf(workspaceId) != (tenant ?? "").ToLowerInvariant();

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

            // a folder in another TopoMojo's workspace is theirs, even when its template id is
            // also known here (an export imported into both keeps its template ids)
            if (IsOtherTenants(workspaceId))
                continue;

            if (templateId is not null && templates.TryGetValue(templateId, out TemplateRef template))
            {
                entry.TemplateName = template.Name;
                workspaceId = NormalizeId(template.WorkspaceId);
            }
            else if (templateId is not null)
            {
                // a copy whose template was deleted
                entry.TemplateName = "(deleted)";

                if (workspaceId is null)
                {
                    entry.WorkspaceName = "(deleted)";
                    report.Sources.Add(entry);
                    continue;
                }
            }

            if (workspaceId is null || workspaceId == globalId)
            {
                entry.IsGlobal = true;
            }
            else
            {
                if (workspaces.TryGetValue(workspaceId, out string name))
                    entry.WorkspaceName = name;
                else if (IsOtherTenants(workspaceId))
                    continue;
                else
                    entry.WorkspaceName = "(deleted)";

                entry.WorkspaceId = workspaceId;
            }

            report.Sources.Add(entry);
        }

        return report;
    }
}
