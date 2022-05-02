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
    public class DispatchValidator : IModelValidator
    {
        private readonly IDispatchStore _store;

        public DispatchValidator(
            IDispatchStore store
        )
        {
            _store = store;
        }

        public Task Validate(object model)
        {

            if (model is Entity)
                return _validate(model as Entity);

            if (model is NewDispatch)
                return _validate(model as NewDispatch);

            if (model is ChangedDispatch)
                return _validate(model as ChangedDispatch);

            if (model is DispatchSearch)
                return _validate(model as DispatchSearch);



            throw new NotImplementedException();
        }

        private async Task _validate(Entity model)
        {
            if (!await Exists(model.Id))
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }

        private async Task _validate(NewDispatch model)
        {
            if (string.IsNullOrEmpty(model.Trigger))
                throw new ArgumentException("Trigger must not be empty.");

            if (await _store.DbContext.Gamespaces.FindAsync(model.TargetGroup) == null)
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }

        private async Task _validate(ChangedDispatch model)
        {
            if (!await Exists(model.Id))
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }

        private async Task _validate(DispatchSearch model)
        {
            if (string.IsNullOrEmpty(model.gs))
                throw new ResourceNotFound();
                
            await Task.CompletedTask;
        }

        private async Task<bool> Exists(string id)
        {
            return
                id.NotEmpty() &&
                (await _store.Retrieve(id)) is Data.Dispatch
            ;
        }

        private async Task<bool> DispatchExists(string id)
        {
            return
                id.NotEmpty() &&
                (await _store.DbContext.Dispatches.FindAsync(id)) is Data.Dispatch
            ;
        }

    }
}
