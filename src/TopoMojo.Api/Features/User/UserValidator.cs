// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Threading.Tasks;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Validators
{
    public class UserValidator: IModelValidator
    {
        private readonly IUserStore _store;

        public UserValidator(
            IUserStore store
        )
        {
            _store = store;
        }

        public Task Validate(object model)
        {
            if (model is Entity)
                return _validate(model as Entity);

            if (model is UserSearch)
                return _validate(model as UserSearch);

            throw new System.NotImplementedException();
        }

        private async Task _validate(UserSearch model)
        {
            await Task.CompletedTask;
        }

        private async Task _validate(Entity model)
        {
            if ((await Exists(model.Id)).Equals(false))
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }

        private async Task<bool> Exists(string id)
        {
            return
                id.NotEmpty() &&
                (await _store.Retrieve(id)) is Data.User
            ;
        }
    }
}
