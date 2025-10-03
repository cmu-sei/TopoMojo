// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services;

public class UserService
(
    ILogger<WorkspaceService> logger,
    IMapper mapper,
    CoreOptions options,
    IUserStore userStore,
    IMemoryCache userCache
) : BaseService(logger, mapper, options), IApiKeyAuthenticationService
{
    private readonly IUserStore _store = userStore;
    private readonly IMemoryCache _cache = userCache;

    public async Task<User[]> List(UserSearch search, CancellationToken ct = default)
    {
        var q = _store.List();
        var term = search.Term?.ToLower();

#pragma warning disable CA1862
        if (term.NotEmpty())
        {
            q = q.Where(p =>
                p.Name.ToLower().Contains(term) ||
                p.Id.StartsWith(term)
            );
        }

        if (search.Scope.NotEmpty())
        {
            q = q.Where(p => p.Scope.Contains(search.Scope));
        }

        if (search.IsServiceAccount is not null)
        {
            q = q.Where(p => (p.ServiceAccountClientId != null && p.ServiceAccountClientId != string.Empty) == search.IsServiceAccount);
        }

        if (search.WantsAdmins)
        {
            q = ApplyRoleFilter(q, UserRole.Administrator);
        }

        if (search.WantsCreators)
        {
            q = ApplyRoleFilter(q, UserRole.Creator);
        }

        if (search.WantsObservers)
        {
            q = ApplyRoleFilter(q, UserRole.Observer);
        }

        if (search.WantsBuilders)
        {
            q = ApplyRoleFilter(q, UserRole.Builder);
        }

        if (search.Skip > 0)
        {
            q = q.Skip(search.Skip);
        }

        if (search.Take > 0)
        {
            q = q.Take(search.Take);
        }

        q = q.OrderBy(p => p.Name);

        var users = await Mapper.ProjectTo<User>(q).ToArrayAsync(ct);
        return users;
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
        return Mapper.Map<User>(await _store.Retrieve(id));
    }

    public async Task<User> AddOrUpdate(ChangedUser model)
    {
        var entity = model.Id.NotEmpty()
            ? await _store.Retrieve(model.Id)
            : null
        ;

        if (entity is not null)
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

        if (entity is not null)
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
            entity.WorkspaceLimit = CoreOptions.DefaultWorkspaceLimit;
            entity.GamespaceLimit = CoreOptions.DefaultGamespaceLimit;
            await _store.Create(entity);
        }

        _cache.Remove(entity.Id);

        return Mapper.Map<User>(entity);
    }

    public async Task<User> FindByServiceAccountClientId(string clientId)
    {
        if (clientId.IsEmpty())
        {
            return null;
        }

        var user = await _store
            .DbContext
            .Users
            .Where(u => u.ServiceAccountClientId == clientId)
            .SingleOrDefaultAsync();

        return user == null ? null : Mapper.Map<User>(user);
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
        await _store.Delete(id);
    }

    public async Task<bool> CanWork(string id)
    {
        var user = await Load(id);
        int count = await _store.WorkspaceCount(id);
        return user.IsCreator || user.WorkspaceLimit > 0 || count > 0;
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
            .Replace('/', '_')
            .Replace('+', '_');

        entity.ApiKeys.Add(new Data.ApiKey
        {
            Id = Guid.NewGuid().ToString("n"),
            Hash = key.ToSha256(),
            Name = subjectName,
            WhenCreated = DateTimeOffset.UtcNow
        });

        await _store.Update(entity);

        return new ApiKeyResult { Value = key };
    }

    public async Task DeleteApiKey(string id)
    {
        await _store.DeleteApiKey(id);
    }

    public async Task<ApiKeyResolvedUser> ResolveApiKey(string key)
    {
        if (key.IsEmpty())
            return null;

        var entity = await _store.ResolveApiKey(key.ToSha256());

        return (entity is not null)
            ? new ApiKeyResolvedUser { Id = entity.Id, Name = entity.Name }
            : null
        ;
    }

    internal async Task<object> LoadUserKeys(string id)
    {
        return Mapper.Map<ApiKey[]>(
            await _store.DbContext.ApiKeys
                .Where(a => a.UserId == id)
                .ToArrayAsync()
        );
    }

    /// <summary>
    /// Among a set of user roles, return the one with the most permissions. This is mainly used to resolve a user's effective role during 
    /// claims transformation, or during mapping to DTO objects. 
    /// 
    /// This is static so it's accessible in mapping contexts.
    /// </summary>
    /// <param name="roles"></param>
    /// <returns>
    ///     The role with the most permissions among roles in the argument. If no roles are specified,
    ///     defaults to the lowest-permission role "User".
    /// </returns>
    public static UserRole ResolveEffectiveRole(UserRole[] roles)
    {
        if (roles is null || roles.Length == 0)
        {
            return UserRole.User;
        }

        return roles.OrderBy(r =>
        {
            return r switch
            {
                UserRole.Administrator => 0,
                UserRole.Creator => 1,
                UserRole.Observer => 2,
                UserRole.Builder => 3,
                _ => 4,
            };
        }).First();
    }

    public static UserRole ResolveEffectiveRole(UserRole appRole, UserRole? lastIdpAssignedRole)
    {
        if (lastIdpAssignedRole is null)
        {
            return appRole;
        }

        return ResolveEffectiveRole([appRole, lastIdpAssignedRole.Value]);
    }

    // Why this is the way it is:
    // A user's effective role in Topo is the "maximum" of their assigned app role (User.Role) and their LastIdpAssignedRole (which could be null).
    // The "maximum" is a numerically arbitrary order, currently in the hierarchy Admin > Creator > Observer > Builder > User. We have no way to represent
    // this in a query context, so our choices are either to hard-code those rules (as we did here) or to bring all users back in memory and call the
    // ResolveEffectiveRole function on them before paging. This way at least we don't pull back potentially thousands of users we don't need.
    private static IQueryable<Data.User> ApplyRoleFilter(IQueryable<Data.User> query, UserRole role)
    {
        if (role == UserRole.Administrator)
        {
            return query.Where(p => p.Role == UserRole.Administrator || p.LastIdpAssignedRole == UserRole.Administrator);
        }

        if (role == UserRole.Creator)
        {
            var greaterThanCreatorRoles = new List<UserRole> { UserRole.Administrator };

            return query.Where
            (
                p =>
                    (p.Role == UserRole.Creator && (p.LastIdpAssignedRole == null || !!greaterThanCreatorRoles.Contains(p.LastIdpAssignedRole.Value))) ||
                    (p.LastIdpAssignedRole == UserRole.Creator && !greaterThanCreatorRoles.Contains(p.Role))
            );
        }

        if (role == UserRole.Observer)
        {
            var greaterThanObserverRoles = new List<UserRole> { UserRole.Creator, UserRole.Administrator };

            return query.Where
            (
                p =>
                    (p.Role == UserRole.Observer && (p.LastIdpAssignedRole == null || !greaterThanObserverRoles.Contains(p.LastIdpAssignedRole.Value))) ||
                    (p.LastIdpAssignedRole == UserRole.Observer && !greaterThanObserverRoles.Contains(p.Role))
            );
        }

        if (role == UserRole.Builder)
        {
            var greaterThanBuilderRoles = new List<UserRole> { UserRole.Observer, UserRole.Creator, UserRole.Administrator };

            return query.Where
            (
                p =>
                    (p.Role == UserRole.Builder && (p.LastIdpAssignedRole == null || !!greaterThanBuilderRoles.Contains(p.LastIdpAssignedRole.Value))) ||
                    (p.LastIdpAssignedRole == UserRole.Builder && !greaterThanBuilderRoles.Contains(p.Role))
            );
        }

        return query;
    }
}
