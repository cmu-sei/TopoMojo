// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Exceptions;

namespace TopoMojo.Api.Validators
{
    public class WorkspaceValidator : IModelValidator
    {
        private readonly IWorkspaceStore _store;

        public WorkspaceValidator(
            IWorkspaceStore store
        )
        {
            _store = store;
        }

        public Task Validate(object model)
        {

            if (model is Entity)
                return _validate(model as Entity);

            if (model is NewWorkspace)
                return _validate(model as NewWorkspace);

            if (model is ChangedWorkspace)
                return _validate(model as ChangedWorkspace);

            if (model is ClientAudience)
                return _validate(model as ClientAudience);

            if (model is ChallengeSpec)
                return _validate(model as ChallengeSpec);

            if (model is WorkspaceSearch)
                return _validate(model as WorkspaceSearch);

            throw new NotImplementedException();
        }

        private async Task _validate(WorkspaceSearch model)
        {
            await Task.CompletedTask;
        }

        private async Task _validate(NewWorkspace model)
        {
            await Task.CompletedTask;
        }

        private async Task _validate(ChangedWorkspace model)
        {
            if (model.Name.IsEmpty())
                throw new ArgumentException("ChangedWorkspace.Name");

            if ((await Exists(model.Id)).Equals(false))
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }

        private async Task _validate(ChallengeSpec model)
        {
            await Task.CompletedTask;
        }

        private async Task _validate(Entity model)
        {
            if ((await Exists(model.Id)).Equals(false))
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }

        private async Task _validate(ClientAudience model)
        {
            if (
                model.Audience.NotEmpty() &&
                model.Scope.Split(' ', ',', ';').Contains(model.Audience).Equals(false)
            )
            {
                throw new InvalidClientAudience();
            }

            await Task.CompletedTask;
        }

        private async Task<bool> Exists(string id)
        {
            return
                id.NotEmpty() &&
                (await _store.Retrieve(id)) is Data.Workspace
            ;
        }
    }
}
