// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;

namespace TopoMojo.Api.Data.Abstractions
{
    public interface IGamespaceStore : IStore<Gamespace>
    {
        IQueryable<Gamespace> ListByUser(string subjectId);
        Task<Gamespace> Load(string id);
        Task<Gamespace> LoadActiveByContext(string workspaceId, string subjectId);
        Task<Player[]> LoadPlayers(string id);
        Task<Player> FindPlayer(string gamespaceId, string subjectId);
        Task DeletePlayer(string gamespaceId, string subjectId);
        Task<bool> CanInteract(string id, string subjectId);
        Task<bool> CanManage(string id, string subjectId);
        Task<bool> HasValidUserScope(string id, string scope, string subjectId);
        Task<bool> IsBelowGamespaceLimit(string id, int gamespaceLimit);
    }
}
