// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;

namespace TopoMojo.Api.Data
{
    public partial class WorkspaceStore(
        TopoMojoDbContext db
        ) : Store<Workspace>(db), IWorkspaceStore
    {
        public override IQueryable<Workspace> List(string term = null)
        {
            var q = base.List();

            if (!string.IsNullOrEmpty(term))
            {
                term = term.ToLower();

#pragma warning disable CA1862
                q = q.Where(t =>
                    t.Name.ToLower().Contains(term) ||
                    t.Tags.ToLower().Contains(term) ||
                    t.Description.ToLower().Contains(term) ||
                    t.Author.ToLower().Contains(term) ||
                    t.Audience.ToLower().Contains(term) ||
                    t.Id.StartsWith(term)
                );
            }

            return q.Include(t => t.Workers);
        }

        public async Task<Workspace> Load(string id)
        {
            return await base.Retrieve(id, query => query
                .Include(t => t.Workers)
                .Include(t => t.Templates).ThenInclude(p => p.Parent)
            );
        }

        public async Task<Gamespace[]> LoadActiveGamespaces(string id)
        {
            return await DbContext.Gamespaces
                .Where(g =>
                    g.WorkspaceId == id &&
                    g.EndTime <= DateTimeOffset.MinValue
                )
                .ToArrayAsync()
            ;
        }

        public async Task<Workspace> LoadWithParents(string id)
        {
            return await base.Retrieve(id, query => query
                .Include(t => t.Workers)
                .Include(t => t.Templates)
                .ThenInclude(o => o.Parent)
            );
        }

        public async Task<Workspace> LoadFromInvitation(string code)
        {
            return await DbSet
                .Include(t => t.Templates)
                .Include(t => t.Workers)
                .Where(t => t.ShareCode == code)
                .SingleOrDefaultAsync();
        }

        public async Task DeleteWithTemplates(string id, Action<IEnumerable<Data.Template>> templateAction)
        {
            var entity = await Retrieve(id, q =>
                q.Include(w => w.Templates)
            );

            templateAction.Invoke(entity.Templates);

            DbSet.Remove(entity);

            await DbContext.SaveChangesAsync();
        }

        public async Task<bool> CanEdit(string id, string subjectId)
        {
            return (await FindWorker(id, subjectId))?.CanEdit ?? false;
        }

        public async Task<bool> CanManage(string id, string subjectId)
        {
            return (await FindWorker(id, subjectId))?.CanManage ?? false;
        }

        public async Task<Worker> FindWorker(string id, string subjectId)
        {
            return await DbContext.Workers.FindAsync(subjectId, id);
        }

        public async Task<int> CheckUserWorkspaceCount(string userId)
        {
            return await DbContext.Workers.CountAsync(w =>
                w.SubjectId == userId &&
                w.Permission.HasFlag(Permission.Manager)
            );
        }

        public async Task<bool> CheckUserWorkspaceLimit(string userId)
        {
            var user = await DbContext.Users.FindAsync(userId);

            int count = await CheckUserWorkspaceCount(userId);

            return count < user.WorkspaceLimit;
        }

        public async Task<int> CheckGamespaceCount(string id)
        {
            return await DbContext.Gamespaces.CountAsync(g =>
                g.WorkspaceId == id &&
                g.EndTime <= DateTimeOffset.MinValue
            );
        }

        public async Task<bool> HasActiveGames(string id)
        {
            return (await CheckGamespaceCount(id)) > 0;
        }

        public async Task<Workspace[]> DeleteStale(DateTimeOffset staleMarker, bool published, bool dryrun = true)
        {
            var query = published
                ? DbSet.Where(w => w.IsPublished || !string.IsNullOrWhiteSpace(w.Audience))
                : DbSet.Where(w => !w.IsPublished && string.IsNullOrWhiteSpace(w.Audience));

            var results = await query
                .Where(g => g.LastActivity < staleMarker)
                .ToArrayAsync();

            if (!dryrun)
            {
                DbSet.RemoveRange(results);

                await DbContext.SaveChangesAsync();
            }

            return results;
        }

        public async Task<Workspace> Clone(string id, string tenantId)
        {
            var entity = await base.List()
                .AsNoTracking()
                .Include(t => t.Templates)
                .Include(t => t.Workers)
                .FirstOrDefaultAsync(w => w.Id == id)
            ;

            string suffix = "-CLONE";
            var match = NameVersionRegex().Match(entity.Name);
            if (match.Success && int.TryParse(match.Groups[1].ValueSpan, out int version))
            {
                suffix = $"v{version + 1}";
                entity.Name = entity.Name[0..match.Index];
            }

            entity.Name += suffix;
            entity.Id = Guid.NewGuid().ToString("n");

            if (string.IsNullOrEmpty(tenantId).Equals(false))
                entity.Id = string.Concat(
                    tenantId,
                    entity.Id.AsSpan(0, entity.Id.Length - tenantId.Length)
                );

            foreach (var worker in entity.Workers)
                worker.WorkspaceId = entity.Id;

            foreach (Template template in entity.Templates)
            {
                string sourceId = template.Id;

                template.Id = Guid.NewGuid().ToString("n");

                if (template.Name.EndsWith(match.Value))
                    template.Name = template.Name[0..^match.Length] + suffix;

                if (template.Iso?.Contains(id) ?? false)
                    template.Iso = "";

                if (template.IsLinked)
                    continue;

                template.ParentId = sourceId;
                template.IsPublished = false;

                var tu = new Services.TemplateUtility(template.Detail);

                tu.LocalizeDiskPaths(entity.Id, template.Id);

                template.Detail = tu.ToString();

            }

            return await Create(entity);
        }

        public IQueryable<Template> ListScopedTemplates()
        {
            return DbContext.Templates
                .Include(t => t.Workspace)
                .Where(t =>
                    t.Audience != null
                )
            ;
        }

        [GeneratedRegex(@"v(\d+)$")]
        private static partial Regex NameVersionRegex();
    }
}
