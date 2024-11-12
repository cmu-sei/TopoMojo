// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services
{
    public class DispatchService(
        IDispatchStore store,
        IMapper mapper
        )
    {
        public async Task<Dispatch> Create(NewDispatch model)
        {
            if (string.IsNullOrEmpty(model.ReferenceId))
                model.ReferenceId = Guid.NewGuid().ToString("n");

            var entity = mapper.Map<Data.Dispatch>(model);

            await store.Create(entity);

            return mapper.Map<Dispatch>(entity);
        }

        public async Task<Dispatch> Retrieve(string id)
        {
            return mapper.Map<Dispatch>(await store.Retrieve(id));
        }

        /// <summary>
        /// Add or Update dispatch reponse
        /// </summary>
        /// <remarks>
        /// If a dispatch is broadcast (no target), then multiple response
        /// can be added.
        /// </remarks>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<Dispatch> Update(ChangedDispatch model)
        {
            model.WhenUpdated = DateTimeOffset.UtcNow;

            var entity = await store.Retrieve(model.Id);

            // targets match (specified or blank)
            if (model.TargetName == entity.TargetName)
            {
                mapper.Map(model, entity);
                await store.Update(entity);
            }
            else if (string.IsNullOrEmpty(entity.TargetName))
            {
                // no target specified, so add new for each response that self identifies its target name
                var replica = mapper.Map<Data.Dispatch>(entity);
                model.Id = null;
                mapper.Map(model, replica);
                await store.Create(replica);
                entity = replica;
            }
            else
            {
                // don't update when target is specified, but response doesn't match
                throw new ActionForbidden();
            }

            return mapper.Map<Dispatch>(entity);
        }

        public async Task Delete(string id)
        {
            await store.Delete(id);
        }

        public async Task<Dispatch[]> List(DispatchSearch filter, CancellationToken ct = default)
        {
            var q = store.List();

            q = q.Where(d => d.TargetGroup == filter.GamespaceId);

            if (DateTimeOffset.TryParse(filter.Since, out DateTimeOffset ts))
                q = q.Where(d => d.WhenCreated > ts || d.WhenUpdated > ts);

            if (filter.WantsPending)
                q = q.Where(d => d.WhenUpdated <= DateTimeOffset.MinValue);

            q = q.OrderBy(d => d.WhenCreated);

            q = q.Skip(filter.Skip);

            if (filter.Take > 0)
                q = q.Take(filter.Take);

            return await mapper.ProjectTo<Dispatch>(q).ToArrayAsync(ct);
        }

    }

}
