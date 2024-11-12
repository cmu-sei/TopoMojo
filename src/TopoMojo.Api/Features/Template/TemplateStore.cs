// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Data
{
    public class TemplateStore(
        TopoMojoDbContext db
        ) : Store<Template>(db), ITemplateStore
    {
        public override IQueryable<Template> List(string term = null)
        {
            if (term.IsEmpty())
                return base.List();

            string x = term.ToLower();

            #pragma warning disable CA1862
            return base.List().Where(t =>
                t.Name.ToLower().Contains(x) ||
                t.Id.StartsWith(x) ||
                t.WorkspaceId.StartsWith(x) ||
                t.Audience.ToLower().Contains(x) ||
                t.Workspace.Name.ToLower().StartsWith(x)
            );
        }

        public async Task<Template> Load(string id)
        {
            return await base.Retrieve(id, query => query
                .Include(tt => tt.Parent)
                .Include(tt => tt.Workspace)
                .ThenInclude(t => t.Workers)
            );
        }

        public async Task<Template> LoadWithParent(string id)
        {
            return await base.Retrieve(id, query => query
                .Include(tt => tt.Parent)
            );
        }

        public async Task<bool> HasDescendents(string id)
        {
            // var entity = await Retrieve(id);

            return await DbSet
                .Where(t => t.ParentId == id)
                .AnyAsync();
        }

        public async Task<Template[]> ListChildren(string parentId)
        {
            return await base.List()
                .Include(t => t.Workspace)
                .Where(t => t.ParentId == parentId)
                .ToArrayAsync();
        }

        public async Task<bool> AtTemplateLimit(string id)
        {
            return await DbContext.Workspaces
                .Where(w => w.Id == id)
                .Select(w => w.Templates.Count >= w.TemplateLimit)
                .FirstOrDefaultAsync()
            ;
        }

        public async Task<string> ResolveKey(string key)
        {
            var name = await DbContext.Workspaces.Where(t => t.Id == key)
                .Select(t => t.Name)
                .SingleOrDefaultAsync();

            if (!string.IsNullOrEmpty(name))
                return "workspace: " + name;

            name = await DbContext.Gamespaces.Where(g => g.Id == key)
                .Select(g => g.Name)
                .SingleOrDefaultAsync();

            if (!string.IsNullOrEmpty(name))
                return "gamespace: " + name;

            return null;
        }
    }
}
