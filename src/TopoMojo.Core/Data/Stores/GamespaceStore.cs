// Copyright 2020 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using TopoMojo.Data.Abstractions;

namespace TopoMojo.Data
{
    public class GamespaceStore : DataStore<Gamespace>, IGamespaceStore
    {
        public GamespaceStore (
            TopoMojoDbContext db,
            IMemoryCache memoryCache,
            IDistributedCache cache = null
        ) : base(db, memoryCache) { }

        public override async Task<Gamespace> Add(Gamespace entity)
        {

            if (entity.Workspace != null)
            {
                entity.Workspace.LaunchCount += 1;

                entity.Workspace.LastActivity = DateTime.UtcNow;
            }

            entity.LastActivity = DateTime.UtcNow;

            var gamespace = await base.Add(entity);

            return gamespace;
        }

        public IQueryable<Gamespace> ListByProfile(int id)
        {
            return DbContext.Players
                .Where(p => p.PersonId == id)
                .Select(p => p.Gamespace);
        }

        public IQueryable<Gamespace> ListByProfile(string id)
        {
            return DbContext.Players
                .Where(p => p.Person.GlobalId == id)
                .Select(p => p.Gamespace);
        }

        public override async Task<Gamespace> Load(int id)
        {
            return await base.Load(id, query => query
                .Include(g => g.Workspace)
                    .ThenInclude(t => t.Templates)
                        .ThenInclude(tm => tm.Parent)
                .Include(g => g.Players)
                    .ThenInclude(w => w.Person)
            );
        }

        public override async Task<Gamespace> Load(string id)
        {
            return await base.Load(id, query => query
                .Include(g => g.Workspace)
                    .ThenInclude(t => t.Templates)
                        .ThenInclude(tm => tm.Parent)
                .Include(g => g.Players)
                    .ThenInclude(w => w.Person)
            );
        }

        public async Task<Gamespace> FindByShareCode(string code)
        {
            int id = await DbContext.Gamespaces
                .Where(g => g.ShareCode == code)
                .Select(g => g.Id)
                .SingleOrDefaultAsync();

            return (id > 0)
                ? await Load(id)
                : null;
        }

        public async Task<Gamespace> FindByContext(int topoId, int profileId)
        {
            int id = await DbContext.Players
                .Where(g => g.PersonId == profileId && g.Gamespace.WorkspaceId == topoId)
                .Select(p => p.GamespaceId)
                .SingleOrDefaultAsync();

            return (id > 0)
                ? await Load(id)
                : null;
        }

        public async Task<Gamespace> FindByPlayer(int playerId)
        {
            int id = await DbContext.Players
                .Where(p => p.Id == playerId)
                .Select(p => p.GamespaceId)
                .SingleOrDefaultAsync();

            return (id > 0)
                ? await Load(id)
                : null;
        }

        public override async Task Delete(int id)
        {
            var entity = await base.Load(id);
            var list = await DbContext.Messages.Where(m => m.RoomId == entity.GlobalId).ToArrayAsync();
            DbContext.Messages.RemoveRange(list);
            await base.Delete(id);
        }

        public async Task<Gamespace[]> DeleteStale(DateTime staleMarker, bool dryrun = true)
        {
            var results = await DbContext.Gamespaces
                .Where(g => g.LastActivity < staleMarker)
                .ToArrayAsync();

            if (!dryrun)
            {
                DbContext.Gamespaces.RemoveRange(results);

                await DbContext.SaveChangesAsync();
            }

            return results;
        }
    }
}
