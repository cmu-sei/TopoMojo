// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TopoMojo.Api.Services
{
    public partial class TemplateService(
        ITemplateStore templateStore,
        IHypervisorService podService,
        ILogger<TemplateService> logger,
        IMapper mapper,
        CoreOptions options
        ) : BaseService(logger, mapper, options)
    {
        private readonly ITemplateStore _store = templateStore;
        private readonly IHypervisorService _pod = podService;

        public async Task<TemplateSummary[]> List(TemplateSearch search, bool sudo, CancellationToken ct = default)
        {
            var q = _store.List(search.Term)
                .Include(t => t.Workspace)
                .Include(t => t.Parent)
                as IQueryable<Data.Template>
            ;

            if (sudo && search.ParentId.NotEmpty())
                q = q.Where(t => t.ParentId == search.ParentId);

            if (!sudo || search.WantsPublished)
                q = q.Where(t => t.IsPublished);

            q = q.OrderBy(t => t.Name);

            if (search.Skip > 0)
                q = q.Skip(search.Skip);

            if (search.Take > 0)
                q = q.Take(search.Take);

            return await Mapper.ProjectTo<TemplateSummary>(q)
                .ToArrayAsync(ct)
            ;
        }

        public async Task<TemplateSummary[]> ListSiblings(string id, bool published)
        {
            var entity = await _store.Load(id);

            if (entity.Parent is null)
                return [];

            var list = await Mapper.ProjectTo<TemplateSummary>(
                _store.List().Where(t => t.ParentId == entity.ParentId)
            ).ToArrayAsync();

            var query = list.Where(t => t.Id != id);

            if (published)
            {
                query = query.Where(t =>
                    t.IsPublished ||
                    t.Audience.HasAnyToken(entity.Workspace?.TemplateScope)
                );
            }

            return query.ToArray();
        }

        public async Task<Template> Load(string id)
        {
            var template = await _store.Retrieve(id);

            return Mapper.Map<Template>(template);
        }

        internal async Task<bool> CanEdit(string id, string actorId)
        {
            return await _store.DbContext.Templates
                .Where(t => t.Id == id)
                .SelectMany(t => t.Workspace.Workers)
                .AnyAsync(w => w.SubjectId == actorId);
        }

        internal async Task<bool> HasValidAudience(string tid, string wid, string actor_scope)
        {
            var entity = await _store.Retrieve(tid);
            var workspace = await _store.DbContext.Workspaces.FindAsync(wid);
            string scope = $"{workspace.TemplateScope} {actor_scope}";
            return entity.IsPublished || entity.Audience.HasAnyToken(scope);
        }

        internal async Task<bool> CanEditWorkspace(string id, string actorId)
        {
            return await _store.DbContext.Workspaces
                .Where(t => t.Id == id)
                .SelectMany(w => w.Workers)
                .AnyAsync(w => w.SubjectId == actorId);
        }

        public async Task<TemplateDetail> LoadDetail(string id)
        {
            var template = await _store.Retrieve(id, q => q.Include(t => t.Parent));

            return Mapper.Map<TemplateDetail>(template);
        }

        public async Task<TemplateDetail> Create(NewTemplateDetail model)
        {
            model.Detail = new TemplateUtility(model.Detail, model.Name).ToString();

            var t = Mapper.Map<Data.Template>(model);

            await _store.Create(t);

            return Mapper.Map<TemplateDetail>(t);
        }

        public async Task<TemplateDetail> Configure(ChangedTemplateDetail template)
        {
            var entity = await _store.Retrieve(template.Id);

            Mapper.Map(template, entity);

            await _store.Update(entity);

            return Mapper.Map<TemplateDetail>(entity);
        }

        public async Task<TemplateDetail> Clone(TemplateClone model)
        {
            var entity = await _store.DbContext.Templates
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == model.Id)
            ;

            string suffix = "-CLONE";
            var match = NameVersionRegex().Match(entity.Name);
            if (match.Success && int.TryParse(match.Groups[1].ValueSpan, out int version))
            {
                suffix = $"v{version + 1}";
                entity.Name = entity.Name[0..match.Index];
            }

            entity.ParentId = entity.Id;
            entity.IsLinked = false;
            entity.Id = Guid.NewGuid().ToString("n");
            entity.Name = model.Name ?? $"{entity.Name}{suffix}";
            entity.IsPublished = false;
            entity.Detail = LocalizeCloneDiskPaths(entity.Detail, entity.WorkspaceId, entity.Id);

            await _store.Create(entity);

            return Mapper.Map<TemplateDetail>(entity);
        }

        /// <summary>
        /// Point a cloned template's detail at its own disks
        /// </summary>
        /// <remarks>
        /// A clone starts as a copy of its source's detail, so without this its disk paths are the
        /// source's disk paths. That leaves the clone unlinked but not independent: deleting it
        /// deletes disks the source still needs. Recording the source's path as each disk's Source
        /// is what makes disk initialization copy the disk rather than share it. This is the same
        /// treatment WorkspaceStore.Clone gives the templates of a cloned workspace.
        /// </remarks>
        internal static string LocalizeCloneDiskPaths(string detail, string workspaceId, string templateId)
        {
            if (detail.IsEmpty())
                return detail;

            var tu = new TemplateUtility(detail);

            // A template with no workspace is stock, and stock disks live in the public folder,
            // which is keyed by the empty guid the same way a workspace folder is keyed by its id.
            tu.LocalizeDiskPaths(
                workspaceId.NotEmpty() ? workspaceId : Guid.Empty.ToString(),
                templateId
            );

            return tu.ToString();
        }

        public async Task<Template> Update(ChangedTemplate template)
        {
            var entity = await _store.Retrieve(template.Id);

            Mapper.Map<ChangedTemplate, Data.Template>(template, entity);

            await _store.Update(entity);

            return Mapper.Map<Template>(await _store.LoadWithParent(entity.Id));
        }

        /// <summary>
        /// associate cloned template to a new source
        /// </summary>
        /// <remarks>
        /// Supports source template revisions by allowing association with a new source
        /// </remarks>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<Template> ReLink(TemplateReLink model)
        {
            var entity = await _store.LoadWithParent(model.TemplateId);
            var source = await _store.LoadWithParent(model.ParentId);

            if (entity.ParentId != source.ParentId)
                throw new TemplateNotPublished();

            entity.ParentId = source.Id;

            await _store.Update(entity);

            return Mapper.Map<Template>(entity);
        }

        public async Task<Template> Link(TemplateLink newlink, bool sudo)
        {
            if (!sudo && await _store.AtTemplateLimit(newlink.WorkspaceId))
                throw new TemplateLimitReached();

            var workspace = await _store.DbContext.Workspaces
                .FirstOrDefaultAsync(w => w.Id == newlink.WorkspaceId)
            ;

            var entity = await _store.Retrieve(newlink.TemplateId);

            string name = entity.Name.Length > 64
                ? entity.Name[..64]
                : entity.Name
            ;

            var newTemplate = new Data.Template
            {
                ParentId = entity.Id,
                IsLinked = true,
                WorkspaceId = workspace.Id,
                Name = $"{name}-{new Random().Next(100, 999)}",
                Description = entity.Description,
                Iso = entity.Iso,
                Networks = entity.Networks,
                Guestinfo = entity.Guestinfo
            };

            await _store.Create(newTemplate);

            return Mapper.Map<Template>(
                await _store.Load(newTemplate.Id)
            );
        }

        public async Task<Template> Unlink(TemplateLink link) //CLONE
        {
            var entity = await _store.LoadWithParent(link.TemplateId);

            if (entity.IsLinked)
            {
                TemplateUtility tu = new(entity.Parent.Detail)
                {
                    Name = entity.Name
                };

                tu.LocalizeDiskPaths(entity.Workspace.Id, entity.Id);

                entity.Detail = tu.ToString();

                entity.IsLinked = false;

                await _store.Update(entity);
            }

            return Mapper.Map<Template>(
                await _store.Load(link.TemplateId)
            );
        }

        public async Task<Template> Delete(string id)
        {
            var entity = await _store.Retrieve(id);

            if (await _store.HasDescendents(id))
                throw new TemplateHasDescendents();

            // delete associated vm
            var deployable = await GetDeployableTemplate(id);

            await _pod.DeleteAll($"{deployable.Name}#{deployable.IsolationTag}");

            // if root template, delete disk(s)
            if (entity.IsLinked.Equals(false))
            {
                string[] shared = ExcludeSharedArtifacts(deployable, await OtherTemplateDetails(id));

                if (shared.Length > 0)
                {
                    Logger.LogWarning(
                        "template {templateId} shares {sharedArtifacts} with another template; leaving them in place",
                        id,
                        string.Join(", ", shared)
                    );
                }

                await _pod.DeleteDisks(deployable);
            }

            await _store.Delete(id);

            return Mapper.Map<Template>(entity);
        }

        /// <summary>
        /// The detail of every template except one
        /// </summary>
        /// <remarks>
        /// Read whole rather than narrowed in the database by the disk path, because a path is stored
        /// inside a json document that escapes it: an apostrophe or a non-ascii character in a path
        /// is a \uXXXX sequence in the column, so a substring predicate would miss the very row that
        /// shares the disk. Deleting a template is rare, and a candidate row has to be parsed to be
        /// believed anyway.
        /// </remarks>
        private async Task<string[]> OtherTemplateDetails(string id)
        {
            return await _store.List()
                .Where(t => t.Id != id && t.Detail != null)
                .Select(t => t.Detail)
                .ToArrayAsync()
            ;
        }

        /// <summary>
        /// Drop the artifacts another template also uses, and name what was dropped
        /// </summary>
        /// <remarks>
        /// Clones created before <see cref="LocalizeCloneDiskPaths"/> carry their source's disk paths
        /// and, on Proxmox, its template name, so deleting one of them would take artifacts the
        /// source still needs. Removing those from the deployable template is what keeps the
        /// hypervisor away from them, and lets the rest of the delete proceed. A detail that cannot
        /// be parsed is read as claiming nothing, so one bad row cannot block every delete.
        /// </remarks>
        internal static string[] ExcludeSharedArtifacts(VmTemplate deployable, IEnumerable<string> otherDetails)
        {
            var others = otherDetails
                .Select(AsTemplateOrNull)
                .Where(t => t is not null)
                .ToArray()
            ;

            var shared = new List<string>();

            if (deployable.Template.NotEmpty() && others.Any(t => SameName(t.Template, deployable.Template)))
            {
                shared.Add(deployable.Template);

                // Proxmox deletes a template's disks by deleting the named template itself, so
                // dropping the name is how it is told there is nothing here of this template's own.
                deployable.Template = null;
            }

            if (deployable.Disks is { Length: > 0 })
            {
                string[] claimed = [.. others
                    .SelectMany(t => t.Disks ?? [])
                    .Select(d => d.Path)
                    .Distinct()
                ];

                var keep = new List<VmDisk>();

                foreach (VmDisk disk in deployable.Disks)
                {
                    if (claimed.Any(path => SameDiskFile(path, disk.Path)))
                        shared.Add(disk.Path);
                    else
                        keep.Add(disk);
                }

                deployable.Disks = [.. keep];
            }

            return [.. shared];
        }

        /// <summary>
        /// Whether two datastore paths name the same disk file
        /// </summary>
        /// <remarks>
        /// Compared by folder and file rather than as whole strings, so that the datastore prefix a
        /// path may or may not carry does not decide whether a disk is shared. Ambiguity resolves
        /// toward shared, because leaving a disk in place is recoverable and deleting one that is
        /// still in use is not.
        /// </remarks>
        internal static bool SameDiskFile(string a, string b)
        {
            if (a.IsEmpty() || b.IsEmpty())
                return false;

            var x = new DatastorePath(a);
            var y = new DatastorePath(b);

            return SameName(x.Folder, y.Folder) && SameName(x.File, y.File);
        }

        private static bool SameName(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// A template detail as a template, or null if it is not one
        /// </summary>
        private static VmTemplate AsTemplateOrNull(string detail)
        {
            // An empty detail would be filled in with a placeholder disk, which is not a claim on
            // anything, and the template it belongs to resolves its real disks through its parent.
            if (detail.IsEmpty())
                return null;

            try
            {
                return new TemplateUtility(detail).AsTemplate();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public async Task<VmTemplate> GetDeployableTemplate(string id, string tag = "")
        {
            var entity = await _store.Load(id);

            string isolationTag = entity.WorkspaceId
                ?? Guid.Empty.ToString()
            ;

            return Mapper.Map<ConvergedTemplate>(entity)
                .ToVirtualTemplate(isolationTag, _pod.GuestSettingSeparators)
                .SetHostAffinity(entity.Workspace?.HostAffinity ?? false)
            ;
        }

        public async Task<Dictionary<string, string>> ResolveKeys(string[] keys)
        {
            var map = new Dictionary<string, string>();

            foreach (string key in keys.Distinct())
            {
                var val = await _store.ResolveKey(key);

                map.Add(key, $"{val ?? "__orphaned"}#{key}");
            }

            return map;
        }

        public async Task<string[]> DiskReport()
        {
            var list = await _store.List().ToArrayAsync();

            return [.. list
                .SelectMany(t =>
                    new TemplateUtility(t.Detail, "").AsTemplate().Disks
                )
                .Select(d => d.Path.Replace("[ds] ", ""))
                .Distinct()
                .OrderBy(x => x)]
            ;
        }

        /// <summary>
        /// Check health by hitting database and hypervisor
        /// </summary>
        /// <param name="id">Template Id</param>
        /// <returns></returns>
        public async Task<bool> CheckHealth(string id)
        {
            var template = await GetDeployableTemplate(id);
            var vm = await _pod.Refresh(template);
            return vm.Status == "initialized"; // healthy is 'initialized' for existing template
        }

        [GeneratedRegex(@"v(\d+)$")]
        private static partial Regex NameVersionRegex();
    }
}
