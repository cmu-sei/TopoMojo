// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Threading.Tasks;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Validators
{
        public class VmValidator : IModelValidator
    {
        private readonly IWorkspaceStore _store;
        private readonly IHypervisorService _pod;

        public VmValidator(
            IHypervisorService pod,
            IWorkspaceStore store
        )
        {
            _pod = pod;
            _store = store;
        }

        public Task Validate(object model)
        {

            if (model is VmOperation)
                return _validate(model as VmOperation);

            throw new NotImplementedException();
        }

        private async Task _validate(VmOperation model)
        {
            if (model.Type != VmOperationType.Save)
            {
                await Task.CompletedTask;
                return;
            }

            string isolationId = model.Id.Contains("#")
                ? model.Id.Tag()
                : (await _pod.Load(model.Id))?.Name.Tag();

            if (await _store.HasActiveGames(isolationId))
                throw new WorkspaceNotIsolated();

            await Task.CompletedTask;
        }
    }
}
