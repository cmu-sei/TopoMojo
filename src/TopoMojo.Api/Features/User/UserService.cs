// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services
{
    public class UserService: _Service, IApiKeyAuthenticationService
    {

        public UserService(
            ILogger<WorkspaceService> logger,
            IMapper mapper,
            CoreOptions options,
            IUserStore userStore,
            IMemoryCache userCache
        ) : base (logger, mapper, options)
        {
            _store = userStore;
            _cache = userCache;
        }

        private readonly IUserStore _store;
        private readonly IMemoryCache _cache;

        public async Task<User[]> List(UserSearch search, CancellationToken ct = default(CancellationToken))
        {
            var q = _store.List();

            string term = search.Term?.ToLower();

            if (term.NotEmpty())
                q = q.Where(p =>
                    p.Name.ToLower().Contains(term) ||
                    p.Id.StartsWith(term)
                );

            if (search.WantsAdmins)
                q = q.Where(p => p.Role == UserRole.Administrator);
            
            if (search.WantsObservers)
                q = q.Where(p => p.Role == UserRole.Observer);

            if (search.WantsCreators)
                q = q.Where(p => p.Role == UserRole.Creator);

            if (search.WantsBuilders)
                q = q.Where(p => p.Role == UserRole.Builder);

            if (search.scope.NotEmpty())
                q = q.Where(p => p.Scope.Contains(search.scope));

            q = q.OrderBy(p => p.Name);

            if (search.Skip > 0)
                q = q.Skip(search.Skip);

            if (search.Take > 0)
                q = q.Take(search.Take);

            return await Mapper.ProjectTo<User>(q).ToArrayAsync(ct);
        }

        public async Task<string[]> ListScopes()
        {
            return (await _store.ListScopes())
                .SelectMany(s => s.Split(' ', ',', ';'))
                .Where(s => s.Length > 0)
                .Distinct()
                .ToArray();
        }

        public async Task<User> Load(string id)
        {
            return Mapper.Map<User>(
                await _store.Retrieve(id)
            );
        }

        public async Task<User> AddOrUpdate(ChangedUser model)
        {
            var entity = model.Id.NotEmpty()
                ? await _store.Retrieve(model.Id)
                : null
            ;

            if (entity is Data.User)
                await _store.Update(
                    Mapper.Map(model, entity)
                );
            else
                entity = await _store.Create(
                    Mapper.Map<Data.User>(model)
                );

            _cache.Remove(entity.Id);

            return Mapper.Map<User>(entity);
        }

        public async Task<User> AddOrUpdate(UserRegistration model)
        {
            var entity = await _store.Retrieve(model.Id);

            if (entity is Data.User)
            {
                if (model.Name.NotEmpty() && entity.Name != model.Id)
                {
                    entity.Name = model.Name;

                    await _store.Update(entity);
                }
            }
            else
            {

                entity = Mapper.Map<Data.User>(model);

                entity.WorkspaceLimit = _options.DefaultWorkspaceLimit;

                entity.GamespaceLimit = _options.DefaultGamespaceLimit;

                // entity.Scope = _options.DefaultUserScope;

                await _store.Create(entity);
            }

            _cache.Remove(entity.Id);

            return Mapper.Map<User>(entity);
        }

        public async Task<WorkspaceSummary[]> LoadWorkspaces(string id)
        {
            return await Mapper.ProjectTo<WorkspaceSummary>(
                _store.DbContext.Workspaces
                .Where(w => w.Workers.Any(p => p.SubjectId == id))
            ).ToArrayAsync();
        }

        public async Task<Gamespace[]> LoadGamespaces(string id)
        {
            return await Mapper.ProjectTo<Gamespace>(
                _store.DbContext.Gamespaces
                .Where(w => w.Players.Any(p => p.SubjectId == id))
            ).ToArrayAsync();
        }

        public async Task Delete(string id)
        {
            var entity = await _store.Retrieve(id);

            await _store.Delete(id);
        }

        public async Task<bool> CanInteract(string subjectId, string isolationId)
        {
            return await _store.CanInteract(subjectId, isolationId);
        }

        public async Task<bool> CanInteractWithAudience(string subjectId, string isolationId)
        {
            return await _store.CanInteractWithAudience(subjectId, isolationId);
        }

        public async Task<ApiKeyResult> CreateApiKey(string id, string subjectName)
        {
            var entity = await _store.Retrieve(id);

            var buffer = new byte[24];

            new Random().NextBytes(buffer);

            string key = Convert.ToBase64String(buffer)
                .Replace('/','_')
                .Replace('+','_')
            ;

            entity.ApiKeys.Add(new Data.ApiKey
            {
                Id = Guid.NewGuid().ToString("n"),
                Hash = key.ToSha256(),
                Name = subjectName,
                WhenCreated = DateTimeOffset.UtcNow
            });

            await _store.Update(entity);

            return new ApiKeyResult
            {
                Value = key
            };
        }

        public async Task DeleteApiKey(string id)
        {
            await _store.DeleteApiKey(id);
        }

        public async Task<string> ResolveApiKey(string key)
        {
            if (key.IsEmpty())
                return null;

            var entity = await _store.ResolveApiKey(key.ToSha256());

            return entity?.Id;
        }

        internal async Task<object> LoadUserKeys(string id)
        {
            return Mapper.Map<ApiKey[]>(
                await _store.DbContext.ApiKeys
                    .Where(a => a.UserId == id)
                    .ToArrayAsync()
            );
        }
    }
}
