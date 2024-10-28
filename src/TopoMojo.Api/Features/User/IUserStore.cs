// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Threading.Tasks;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Data.Abstractions
{
    public interface IUserStore : IStore<User>
    {
        Task<bool> CanInteract(string id, string isolationId);
        Task<bool> CanInteractWithAudience(string scope, string isolationId);
        Task<User> LoadWithKeys(string id);
        Task<User> ResolveApiKey(string hash);
        Task DeleteApiKey(string id);
        Task<string[]> ListScopes();
        Task<int> WorkspaceCount(string id);
    }
}
