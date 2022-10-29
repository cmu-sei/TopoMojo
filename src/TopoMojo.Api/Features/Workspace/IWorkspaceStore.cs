// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TopoMojo.Api.Data.Abstractions
{
    public interface IWorkspaceStore : IStore<Workspace>
    {
        Task<Workspace> Load(string id);
        Task<Workspace> LoadFromInvitation(string code);
        Task<Workspace> LoadWithParents(string id);
        Task<Gamespace[]> LoadActiveGamespaces(string id);
        Task<Worker> FindWorker(string id, string subjectId);
        Task<bool> CanEdit(string id, string subjectId);
        Task<bool> CanManage(string id, string subjectId);
        Task<int> CheckUserWorkspaceCount(string profileId);
        Task<bool> CheckUserWorkspaceLimit(string userId);
        Task<bool> HasActiveGames(string id);
        Task<Workspace[]> DeleteStale(DateTimeOffset staleAfter, bool published, bool dryrun = true);
        Task<Workspace> Clone(string id, string tenantId);
        IQueryable<Template> ListScopedTemplates();
        Task<int> CheckGamespaceCount(string id);
        Task DeleteWithTemplates(string id, Action<IEnumerable<Template>> templateAction);
    }
}
