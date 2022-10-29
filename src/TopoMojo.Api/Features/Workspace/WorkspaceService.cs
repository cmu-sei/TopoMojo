// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Services
{
    public class WorkspaceService : _Service
    {
        public WorkspaceService(
            IWorkspaceStore workspaceStore,
            IGamespaceStore gamespaceStore,
            IHypervisorService podService,
            ILogger<WorkspaceService> logger,
            IMapper mapper,
            CoreOptions options
        ) : base (logger, mapper, options)
        {
            _store = workspaceStore;
            _gamespaceStore = gamespaceStore;
            _pod = podService;
        }

        private readonly IWorkspaceStore _store;
        private readonly IGamespaceStore _gamespaceStore;
        private readonly IHypervisorService _pod;

        /// <summary>
        /// List workspace summaries.
        /// </summary>
        /// <returns>Array of WorkspaceSummary</returns>
        public async Task<WorkspaceSummary[]> List(WorkspaceSearch search, string subjectId, bool sudo, CancellationToken ct = default(CancellationToken))
        {
            search.scope += " " + _options.DefaultUserScope;

            // TODO: change this to use tags (efcore many-to-many)

            if (search.WantsPlayable)
                return await ListPlayable(search, ct);

            var q = _store.List(search.Term);

            if (search.WantsAudience)
                q = q.Where(w => w.Audience.Contains(search.aud));

            else if (!sudo)
                q = q.Where(p => p.Workers.Any(w => w.SubjectId == subjectId));

            q = search.Sort == "age"
                ? q.OrderByDescending(w => w.WhenCreated)
                : q.OrderBy(w => w.Name);

            if (search.Skip > 0)
                q = q.Skip(search.Skip);

            if (search.Take > 0)
                q = q.Take(search.Take);

            return Mapper.Map<WorkspaceSummary[]>(
                await q.ToArrayAsync(ct)
            );
        }

        private async Task<WorkspaceSummary[]> ListPlayable(WorkspaceSearch search, CancellationToken ct = default(CancellationToken))
        {
            // TODO: change this to use tags (efcore many-to-many)

            var scopes = search.scope.Split(
                AppConstants.InlineListSeparators,
                StringSplitOptions.RemoveEmptyEntries
            );

            var data = await _store.List(search.Term)
                .Where(w => !string.IsNullOrEmpty(w.Audience))
                .ToArrayAsync()
            ;

            var q = data.AsQueryable()
                .Where(w => w.Audience.HasAnyToken(search.scope))
            ;

            q = search.Sort == "age"
                ? q.OrderByDescending(w => w.WhenCreated)
                : q.OrderBy(w => w.Name);

            if (search.Skip > 0)
                q = q.Skip(search.Skip);

            if (search.Take > 0)
                q = q.Take(search.Take);

            return Mapper.Map<WorkspaceSummary[]>(
                q
            );

        }

        /// <summary>
        /// Lists workspaces with template detail.  This should only be exposed to priviledged users.
        /// </summary>
        /// <returns>Array of Workspaces</returns>
        public async Task<Workspace[]> ListDetail(Search search, CancellationToken ct = default(CancellationToken))
        {
            var q = _store.List(search.Term);

            q = q.Include(t => t.Templates)
                    .Include(t => t.Workers)
                    // .ThenInclude(w => w.Person)
            ;

            q = search.Sort == "age"
                ? q.OrderByDescending(w => w.WhenCreated)
                : q.OrderBy(w => w.Name);

            if (search.Skip > 0)
                q = q.Skip(search.Skip);

            if (search.Take > 0)
                q = q.Take(search.Take);

            return Mapper.Map<Workspace[]>(
                await q.ToArrayAsync(ct)
            );
        }

        public async Task<Boolean> CheckWorkspaceLimit(string id)
        {
            return await _store.CheckUserWorkspaceLimit(id);
        }

        public async Task<Workspace> Load(string id)
        {
            Data.Workspace entity = await _store.Load(id);

            return Mapper.Map<Workspace>(entity);
        }

        /// <summary>
        /// Create a new workspace
        /// </summary>
        /// <param name="model"></param>
        /// <param name="subjectId"></param>
        /// <param name="subjectName"></param>
        /// <returns>Workspace</returns>
        public async Task<Workspace> Create(NewWorkspace model, string subjectId, string subjectName)
        {
            var workspace = Mapper.Map<Data.Workspace>(model);
            
            workspace.Id = Guid.NewGuid().ToString("n");
            workspace.TemplateLimit = _options.DefaultTemplateLimit;
            workspace.WhenCreated = DateTimeOffset.UtcNow;
            workspace.LastActivity = DateTimeOffset.UtcNow;

            if (workspace.Challenge.IsEmpty())
                workspace.Challenge = JsonSerializer.Serialize<ChallengeSpec>(
                    new ChallengeSpec(),
                    jsonOptions
                );

            workspace.Workers.Add(new Data.Worker
            {
                SubjectId = subjectId,
                SubjectName = subjectName,
                Permission = Permission.Manager
            });
            
            if (string.IsNullOrEmpty(_options.Tenant).Equals(false))
                workspace.Id = _options.Tenant + workspace.Id.Substring(0, workspace.Id.Length - _options.Tenant.Length);
            
            workspace = await _store.Create(workspace);

            // TODO: consider handling document here

            return Mapper.Map<Workspace>(workspace);
        }

        public async Task<Workspace> Clone(string id)
        {
            return Mapper.Map<Workspace>(
                await _store.Clone(id, _options.Tenant)
            );
        }

        /// <summary>
        /// Update an existing workspace.
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<Workspace> Update(RestrictedChangedWorkspace model)
        {
            var entity = await _store.Retrieve(model.Id);

            Mapper.Map<RestrictedChangedWorkspace, Data.Workspace>(
                model,
                entity
            );

            await _store.Update(entity);

            return Mapper.Map<Workspace>(entity);
        }

        /// <summary>
        /// Update an existing workspace (privileged).
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public async Task<Workspace> Update(ChangedWorkspace model)
        {
            var entity = await _store.Retrieve(model.Id);

            Mapper.Map<ChangedWorkspace, Data.Workspace>(
                model,
                entity
            );

            await _store.Update(entity);

            return Mapper.Map<Workspace>(entity);
        }

        public async Task<ChallengeSpec> GetChallenge(string id)
        {
            var entity = await _store.Retrieve(id);

            string spec = entity.Challenge ??
                JsonSerializer.Serialize<ChallengeSpec>(new ChallengeSpec());

            return JsonSerializer.Deserialize<ChallengeSpec>(spec, jsonOptions);
        }

        public async Task UpdateChallenge(string id, ChallengeSpec spec)
        {
            var entity = await _store.Retrieve(id);

            entity.Challenge = JsonSerializer.Serialize<ChallengeSpec>(spec, jsonOptions);

            await _store.Update(entity);
        }

        /// <summary>
        /// Delete a workspace
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Workspace</returns>
        public async Task<Workspace> Delete(string id)
        {
            var entity = await _store.Retrieve(id);

            await _pod.DeleteAll(id);

            await _store.DeleteWithTemplates(id, async templates => {

                var disktasks = Mapper.Map<ConvergedTemplate[]>(templates)
                    .Select(ct => _pod.DeleteDisks(ct.ToVirtualTemplate()))
                    .ToArray()
                ;

                await Task.WhenAll(disktasks);

            });

            return Mapper.Map<Workspace>(entity);
        }

        /// <summary>
        /// Determine if subject can edit workspace.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="subjectId"></param>
        /// <returns></returns>
        public async Task<bool> CanEdit(string id, string subjectId)
        {
            return await _store.CanEdit(id, subjectId);
        }

        /// <summary>
        /// Determine if subject can manage workspace.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="subjectId"></param>
        /// <returns></returns>
        public async Task<bool> CanManage(string id, string subjectId)
        {
            return await _store.CanManage(id, subjectId);
        }

        /// <summary>
        /// Generate a new invitation code for a workspace.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<JoinCode> Invite(string id)
        {
            var workspace = await _store.Retrieve(id);

            workspace.ShareCode = Guid.NewGuid().ToString("N");

            await _store.Update(workspace);

            return Mapper.Map<JoinCode>(workspace);
        }

        public async Task<TemplateSummary[]> GetScopedTemplates(string id)
        {
            var workspace = await _store.Retrieve(id);

            if (workspace.TemplateScope.IsEmpty())
                return new TemplateSummary[] {};

            var templates = (await _store.ListScopedTemplates().ToArrayAsync())
                .Where(t => t.Audience.HasAnyToken(workspace.TemplateScope))
                .ToArray()
            ;

            return Mapper.Map<TemplateSummary[]>(templates);

        }

        /// <summary>
        /// Redeem an invitation code to join user to workspace.
        /// </summary>
        /// <param name="code"></param>
        /// <param name="subjectId"></param>
        /// <param name="subjectName"></param>
        /// <returns></returns>
        public async Task<WorkspaceSummary> Enlist(string code, string subjectId, string subjectName)
        {
            var workspace = await _store.LoadFromInvitation(code);

            if (workspace == null)
                throw new ResourceNotFound();

            if (!workspace.Workers.Where(m => m.SubjectId == subjectId).Any())
            {
                workspace.Workers.Add(new Data.Worker
                {
                    SubjectId = subjectId,
                    SubjectName = subjectName,
                    Permission = workspace.Workers.Count > 0
                        ? Permission.Editor
                        : Permission.Manager
                });

                await _store.Update(workspace);
            }

            return Mapper.Map<WorkspaceSummary>(workspace);
        }

        /// <summary>
        /// Remove a worker from a workspace.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="subjectId"></param>
        /// <param name="sudo"></param>
        /// <returns></returns>
        public async Task Delist(string id, string subjectId, bool sudo)
        {
            var workspace = await _store.Load(id);

            var member = workspace.Workers
                .Where(p => p.SubjectId == subjectId)
                .SingleOrDefault();

            if (member == null)
                return;

            int managers = workspace.Workers
                .Count(w => w.Permission.HasFlag(Permission.Manager));

            // Only admins can remove the last remaining workspace manager
            if (!sudo
                && member.CanManage
                && managers == 1)
                throw new ActionForbidden();

            workspace.Workers.Remove(member);

            await _store.Update(workspace);
        }

        /// <summary>
        /// Retrieve existing gamestates for a workspace.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<WorkspaceStats> GetStats(string id)
        {
            var workspace = await _store.Retrieve(id);

            int activeGameCount = await _store.CheckGamespaceCount(id);

            return new WorkspaceStats
            {
                Id = id,
                LastActivity = workspace.LastActivity,
                LaunchCount = workspace.LaunchCount,
                ActiveGamespaceCount = activeGameCount
            };
        }

        /// <summary>
        /// Delete all existing gamespaces of a workspace.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<GameState[]> KillGames(string id)
        {
            var gamespaces = await _store.LoadActiveGamespaces(id);

            foreach (var gamespace in gamespaces)
            {
                await _pod.DeleteAll(gamespace.Id);

                await _gamespaceStore.Delete(gamespace.Id);
            }

            return Mapper.Map<GameState[]>(
                Mapper.Map<GameStateSummary[]>(gamespaces)
            );
        }
    }
}
