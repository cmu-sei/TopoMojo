// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Data
{
    public class UserStore(
        TopoMojoDbContext db
        ) : Store<User>(db), IUserStore
    {
        public override async Task<User> Create(User user)
        {
            string name = user.Name.ExtractBefore("@");

            if (name.IsEmpty())
                user.Name = "Anonymous";

            if (user.Id.IsEmpty())
                user.Id = Guid.NewGuid().ToString();

            user.WhenCreated = DateTimeOffset.UtcNow;

            if (!await DbSet.AnyAsync())
                user.Role = UserRole.Administrator;

            return await base.Create(user);
        }

        public async Task<bool> CanInteract(string userId, string isolationId)
        {
            if (isolationId.IsEmpty() || userId.IsEmpty())
                return false;

            bool found = await DbContext.Players.AnyAsync(w =>
                w.SubjectId == userId &&
                w.GamespaceId == isolationId
            );

            if (found.Equals(false))
                found = await DbContext.Gamespaces.AnyAsync(g =>
                    g.Id == isolationId &&
                    g.ManagerId == userId
                );

            if (found.Equals(false))
                found = await DbContext.Workers.AnyAsync(w =>
                    w.SubjectId == userId &&
                    w.WorkspaceId == isolationId
                );

            return found;
        }

        public async Task<bool> CanInteractWithAudience(string scope, string isolationId)
        {
            if (isolationId.IsEmpty())
                return false;

            var gamespace = await DbContext.Gamespaces
                .Include(g => g.Workspace)
                .FirstOrDefaultAsync(g =>
                    g.Id == isolationId
                );
            if (gamespace != null)
                return gamespace.Workspace.Audience.HasAnyToken(scope);

            var workspace = await DbContext.Workspaces
                .FirstOrDefaultAsync(g =>
                    g.Id == isolationId
                );
            if (workspace != null)
                return workspace.Audience.HasAnyToken(scope);

            return false;
        }

        public Task<User> LoadWithKeys(string id)
        {
            return DbSet
                .Include(u => u.ApiKeys)
                .FirstOrDefaultAsync(
                    u => u.Id == id
                )
            ;
        }

        public async Task DeleteApiKey(string id)
        {
            var entity = await DbContext.ApiKeys.FindAsync(id);

            if (entity == null)
                return;

            DbContext.ApiKeys.Remove(entity);

            await DbContext.SaveChangesAsync();
        }

        public async Task<User> ResolveApiKey(string hash)
        {
            var user = await DbSet.FirstOrDefaultAsync(u =>
                u.ApiKeys.Any(k => k.Hash == hash)
            );

            if (user is not null)
                return user;

            var gs = await DbContext.Gamespaces.FirstOrDefaultAsync(
                g => g.GraderKey == hash
            );

            return (gs is not null)
                ? new User { Id = gs.Id, Name = "Gamespace Agent" }
                : null
            ;

        }

        public async Task<string[]> ListScopes()
        {
            return await DbSet
                .Where(u => !string.IsNullOrEmpty(u.Scope))
                .Select(u => u.Scope)
                .ToArrayAsync()
            ;
        }

        public async Task<int> WorkspaceCount(string id)
        {
            return await DbContext.Workers.CountAsync(w => w.SubjectId == id);
        }
    }
}
