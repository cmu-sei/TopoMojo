// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using System;
using System.Collections.Generic;

namespace TopoMojo.Api.Data
{
    public class Store<TEntity> : IStore<TEntity>
        where TEntity : class, IEntity
    {
        public Store(
            TopoMojoDbContext dbContext
        )
        {
            DbContext = dbContext;
            DbSet = DbContext.Set<TEntity>();
        }

        public TopoMojoDbContext DbContext { get; private set; }
        public DbSet<TEntity> DbSet { get; private set; }

        public virtual IQueryable<TEntity> List(string term = null)
        {
            return DbContext.Set<TEntity>();
        }

        public virtual async Task<TEntity> Create(TEntity entity)
        {
            if (string.IsNullOrWhiteSpace(entity.Id))
                entity.Id = Guid.NewGuid().ToString("n");

            if (entity.WhenCreated == DateTimeOffset.MinValue)
                entity.WhenCreated = DateTimeOffset.UtcNow;

            DbContext.Add(entity);

            await DbContext.SaveChangesAsync();

            return entity;
        }

        public virtual async Task<IEnumerable<TEntity>> Create(IEnumerable<TEntity> range)
        {
            foreach (var entity in range)
                if (string.IsNullOrWhiteSpace(entity.Id))
                    entity.Id = Guid.NewGuid().ToString("n");

            DbContext.AddRange(range);

            await DbContext.SaveChangesAsync();

            return range;
        }

        public virtual async Task<TEntity> Retrieve(string id, Func<IQueryable<TEntity>, IQueryable<TEntity>> includes = null)
        {
            if (includes == null)
                return await DbContext.Set<TEntity>().FindAsync(id);

            return await includes.Invoke(DbContext.Set<TEntity>())
                .Where(e => e.Id == id)
                .SingleOrDefaultAsync()
            ;
        }

        public virtual async Task Update(TEntity entity)
        {
            DbContext.Update(entity);

            await DbContext.SaveChangesAsync();
        }

        public virtual async Task Update(IEnumerable<TEntity> range)
        {
            DbContext.UpdateRange(range);

            await DbContext.SaveChangesAsync();
        }

        public virtual async Task Delete(string id)
        {
            var entity = await Retrieve(id);

            if (entity is TEntity)
            {
                DbContext.Set<TEntity>().Remove(entity);

                await DbContext.SaveChangesAsync();
            }
        }

    }
}
