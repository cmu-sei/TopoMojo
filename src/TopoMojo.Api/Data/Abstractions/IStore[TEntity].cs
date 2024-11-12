// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Data.Abstractions
{
    public interface IStore<TEntity>
        where TEntity : class, IEntity
    {
        TopoMojoDbContext DbContext { get; }

        IQueryable<TEntity> List(string term = null);

        Task<TEntity> Create(TEntity entity);

        Task<IEnumerable<TEntity>> Create(IEnumerable<TEntity> range);

        Task<TEntity> Retrieve(string id, Func<IQueryable<TEntity>, IQueryable<TEntity>> includes = null);

        Task Update(TEntity entity);

        Task Update(IEnumerable<TEntity> range);

        Task Delete(string id);

    }

}
