using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
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
            var entity = Mapper.Map<Data.Dispatch>(model);
            await Store.Create(entity);
            return Mapper.Map<Dispatch>(entity);
        }

        public async Task<Dispatch> Retrieve(string id)
        {
            return Mapper.Map<Dispatch>(await Store.Retrieve(id));
        }

        public async Task Update(ChangedDispatch model)
        {
            model.ResponseTime = DateTimeOffset.Now;
            var entity = await Store.Retrieve(model.Id);
            Mapper.Map(model, entity);
            await Store.Update(entity);
        }

        public async Task Delete(string id)
        {
            await Store.Delete(id);
        }

        public async Task<Dispatch[]> List(DispatchSearch filter,  CancellationToken ct = default(CancellationToken))
        {
            if (!DateTimeOffset.TryParse(filter.since, out DateTimeOffset ts))
                ts = DateTimeOffset.MinValue;

            var q = Store.List();

            q = q.Where(d => 
                d.TargetGroup == filter.gs &&
                (d.WhenCreated > ts || d.WhenUpdated > ts)
            );

            q = q.OrderByDescending(d => d.WhenCreated);
            
            q = q.Skip(filter.Skip);

            if (filter.Take > 0)
                q = q.Take(filter.Take);

            return await Mapper.ProjectTo<Dispatch>(q).ToArrayAsync();
        }

    }

}
