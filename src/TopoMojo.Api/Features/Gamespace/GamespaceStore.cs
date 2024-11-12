// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Data
{
    public class GamespaceStore(
        TopoMojoDbContext db
        ) : Store<Gamespace>(db), IGamespaceStore
    {
        public override IQueryable<Gamespace> List(string term = null)
        {
            if (term.IsEmpty())
                return base.List();

            term = term.ToLower();

            #pragma warning disable CA1862
            return base.List()
                .Where(g =>
                    g.Name.ToLower().Contains(term) ||
                    g.ManagerName.ToLower().Contains(term) ||
                    g.Id.ToLower().StartsWith(term)
                )
            ;
        }

        public IQueryable<Gamespace> ListByUser(string subjectId)
        {
            return base.List()
                .Where(g =>
                    g.ManagerId == subjectId ||
                    g.Players.Any(p => p.SubjectId == subjectId)
                )
            ;
        }

        public override async Task<Gamespace> Create(Gamespace entity)
        {

            if (entity.Workspace != null)
            {
                entity.Workspace.LaunchCount += 1;

                entity.Workspace.LastActivity = DateTimeOffset.UtcNow;
            }

            var gamespace = await base.Create(entity);

            return gamespace;
        }

        public async Task<Gamespace> Load(string id)
        {
            return await base.Retrieve(id, query => query
                .Include(g => g.Players)
                .Include(g => g.Workspace)
                    .ThenInclude(t => t.Templates)
                        .ThenInclude(tm => tm.Parent)
            );
        }

        public async Task<Gamespace> LoadActiveByContext(string workspaceId, string subjectId)
        {
            var ts = DateTimeOffset.UtcNow;

            string id = await DbSet.Where(g =>
                    g.WorkspaceId == workspaceId &&
                    g.EndTime <= DateTimeOffset.MinValue &&
                    g.ExpirationTime > ts &&
                    (
                        g.ManagerId == subjectId ||
                        g.Players.Any(p => p.SubjectId == subjectId)
                    )
                )
                .Select(p => p.Id)
                .FirstOrDefaultAsync()
            ;

            return (!string.IsNullOrEmpty(id))
                ? await Load(id)
                : null;
        }

        public async Task<Player[]> LoadPlayers(string id)
        {
            return await DbContext.Players
                .Where(p => p.Gamespace.Id == id)
                .ToArrayAsync()
            ;
        }

        public async Task<bool> CanInteract(string id, string subjectId)
        {
            return await DbSet.AnyAsync(g =>
                g.Id == id &&
                (
                    g.ManagerId == subjectId ||
                    g.Players.Any(p => p.SubjectId == subjectId)
                )
            );
        }

        public async Task<bool> CanManage(string id, string subjectId)
        {
            return await DbSet.AnyAsync(g =>
                g.Id == id &&
                (
                    g.ManagerId == subjectId ||
                    g.Players.Any(p => p.SubjectId == subjectId && p.Permission == Permission.Manager)
                )
            );
        }

        public async Task<bool> HasValidUserScope(string workspaceId, string scope, string subjectId)
        {
            var workspace = await DbContext.Workspaces.FindAsync(workspaceId);

            return
                workspace.Audience.HasAnyToken(scope) ||
                await DbContext.Workers.AnyAsync(
                    w => w.WorkspaceId ==workspaceId &&
                    w.SubjectId == subjectId
                )
            ;
        }

        public async Task<bool> HasValidUserScopeGamespace(string gamespaceId, string scope)
        {
            var gamespace = await DbContext.Gamespaces
                .Where(g => g.Id == gamespaceId)
                .Include( g => g.Workspace)
                .FirstOrDefaultAsync();

            return gamespace.Workspace.Audience.HasAnyToken(scope);
        }

        public async Task<bool> IsBelowGamespaceLimit(string subjectId, int limit)
        {
            if (limit == 0)
                return true;

            int active = await DbSet.CountAsync(g =>
                g.ManagerId == subjectId &&
                g.StartTime > DateTimeOffset.MinValue &&
                g.EndTime <= DateTimeOffset.MinValue
            );

            return active < limit;
        }

        public async Task<Player> FindPlayer(string id, string subjectId)
        {
            return await DbContext.Players.FindAsync(subjectId, id);
        }

        public async Task DeletePlayer(string id, string subjectId)
        {
            var player = await FindPlayer(id, subjectId);

            if (player is not null)
            {
                DbContext.Players.Remove(player);
                await DbContext.SaveChangesAsync();
            }
        }
    }
}
