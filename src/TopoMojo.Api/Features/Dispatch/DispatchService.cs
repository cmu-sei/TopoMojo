// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services
{
    public class DispatchService
    {
        IDispatchStore Store { get; }
        IMapper Mapper { get; }

        public DispatchService (
            IDispatchStore store,
            IMapper mapper
        ){
            Store = store;
            Mapper = mapper;
        }

        public async Task<Dispatch> Create(NewDispatch model)
        {
            if (string.IsNullOrEmpty(model.ReferenceId))
                model.ReferenceId = Guid.NewGuid().ToString("n");

            var entity = Mapper.Map<Data.Dispatch>(model);

            await Store.Create(entity);

            return Mapper.Map<Dispatch>(entity);
        }

        public async Task<Dispatch> Retrieve(string id)
        {
            return Mapper.Map<Dispatch>(await Store.Retrieve(id));
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

            var entity = await Store.Retrieve(model.Id);

            // targets match (specified or blank)
            if (model.TargetName == entity.TargetName)
            {
                Mapper.Map(model, entity);
                await Store.Update(entity);
            }
            else if (string.IsNullOrEmpty(entity.TargetName))
            {   
                // no target specified, so add new for each response that self identifies its target name
                var replica = Mapper.Map<Data.Dispatch>(entity);
                model.Id = null;
                Mapper.Map(model, replica);
                await Store.Create(replica);
                entity = replica;
            }
            else
            {
                // don't update when target is specified, but response doesn't match
                throw new ActionForbidden();
            }

            return Mapper.Map<Dispatch>(entity);
        }

        public async Task Delete(string id)
        {
            await Store.Delete(id);
        }

        public async Task<Dispatch[]> List(DispatchSearch filter,  CancellationToken ct = default(CancellationToken))
        {
            var q = Store.List();

            q = q.Where(d => d.TargetGroup == filter.gs);

            if (DateTimeOffset.TryParse(filter.since, out DateTimeOffset ts))
                q = q.Where(d => d.WhenCreated > ts || d.WhenUpdated > ts);

            if (filter.WantsPending)
                q = q.Where(d => d.WhenUpdated <= DateTimeOffset.MinValue);

            q = q.OrderBy(d => d.WhenCreated);
            
            q = q.Skip(filter.Skip);

            if (filter.Take > 0)
                q = q.Take(filter.Take);

            return await Mapper.ProjectTo<Dispatch>(q).ToArrayAsync();
        }

    }

}
