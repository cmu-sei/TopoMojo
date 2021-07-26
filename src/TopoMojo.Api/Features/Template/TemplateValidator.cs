// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Validators
{
    public class TemplateValidator : IModelValidator
    {
        private readonly ITemplateStore _store;

        public TemplateValidator(
            ITemplateStore store
        )
        {
            _store = store;
        }

        public Task Validate(object model)
        {

            if (model is Entity)
                return _validate(model as Entity);

            if (model is ChangedTemplate)
                return _validate(model as ChangedTemplate);

            if (model is TemplateLink)
                return _validate(model as TemplateLink);

            if (model is NewTemplateDetail)
                return _validate(model as NewTemplateDetail);

            if (model is TemplateDetail)
                return _validate(model as TemplateDetail);

            if (model is TemplateSearch)
                return _validate(model as TemplateSearch);

            throw new NotImplementedException();
        }

        private async Task _validate(TemplateSearch model)
        {
            await Task.CompletedTask;
        }

        private async Task _validate(TemplateDetail model)
        {
            if ((await Exists(model.Id)).Equals(false))
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }

        private async Task _validate(NewTemplateDetail model)
        {
            if ((await Exists(model.Id)).Equals(true))
                throw new ResourceAlreadyExists();

            await Task.CompletedTask;
        }

        private async Task _validate(TemplateLink model)
        {
            if ((await Exists(model.TemplateId)).Equals(false))
                throw new ResourceNotFound();

            if ((await _store.DbContext.Workspaces.FindAsync(model.WorkspaceId)) == null)
                throw new ResourceNotFound();

            await Task.CompletedTask;
        }

        private async Task _validate(ChangedTemplate model)
        {
            if (model.Name.IsEmpty())
                throw new ArgumentException("ChangedTemplate.Name");

            if ((await Exists(model.Id)).Equals(false))
                throw new ResourceNotFound();

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
                (await _store.Retrieve(id)) is Data.Template
            ;
        }
    }
}
