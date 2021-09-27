// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services
{
    public class TemplateService : _Service
    {
        public TemplateService(
            ITemplateStore templateStore,
            IHypervisorService podService,
            ILogger<TemplateService> logger,
            IMapper mapper,
            CoreOptions options
        ) : base(logger, mapper, options)
        {
            _store = templateStore;

            _pod = podService;
        }

        private readonly ITemplateStore _store;
        private readonly IHypervisorService _pod;

        public async Task<TemplateSummary[]> List(TemplateSearch search, bool sudo, CancellationToken ct = default(CancellationToken))
        {
            var q = _store.List(search.Term)
                .Include(t => t.Workspace) as IQueryable<Data.Template>;

            if (sudo && search.pid.NotEmpty())
                q = q.Where(t => t.ParentId == search.pid);

            if (!sudo || search.WantsPublished)
                q = q.Where(t => t.IsPublished && t.Audience == null);

            q = q.OrderBy(t => t.Name);

            if (search.Skip > 0)
                q = q.Skip(search.Skip);

            if (search.Take > 0)
                q = q.Take(search.Take);

            return Mapper.Map<TemplateSummary[]>(
                await q.ToArrayAsync(ct)
            );
        }

        public async Task<Template> Load(string id)
        {
            var template = await _store.Retrieve(id);

            return Mapper.Map<Template>(template);
        }

        internal async Task<bool> CanEdit(string id, string actorId)
        {
            return await _store.DbContext.Templates
                .Where(t => t.Id == id)
                .SelectMany(t => t.Workspace.Workers)
                .AnyAsync(w => w.SubjectId == actorId);
        }

        internal async Task<bool> CanEditWorkspace(string id, string actorId)
        {
            return await _store.DbContext.Workspaces
                .Where(t => t.Id == id)
                .SelectMany(w => w.Workers)
                .AnyAsync(w => w.SubjectId == actorId);
        }

        public async Task<TemplateDetail> LoadDetail(string id)
        {
            var template = await _store.Retrieve(id);

            return Mapper.Map<TemplateDetail>(template);
        }

        public async Task<TemplateDetail> Create(NewTemplateDetail model)
        {
            model.Detail = new TemplateUtility(model.Detail, model.Name).ToString();

            var t = Mapper.Map<Data.Template>(model);

            await _store.Create(t);

            return Mapper.Map<TemplateDetail>(t);
        }

        public async Task<TemplateDetail> Configure(TemplateDetail template)
        {
            var entity = await _store.Retrieve(template.Id);

            Mapper.Map<TemplateDetail, Data.Template>(template, entity);

            await _store.Update(entity);

            return Mapper.Map<TemplateDetail>(entity);
        }

        public async Task<Template> Update(ChangedTemplate template)
        {
            var entity = await _store.Retrieve(template.Id);

            Mapper.Map<ChangedTemplate, Data.Template>(template, entity);

            await _store.Update(entity);

            return Mapper.Map<Template>(entity);
        }

        public async Task<Template> Link(TemplateLink newlink, bool sudo)
        {
            var entity = await _store.Retrieve(newlink.TemplateId);

            if (entity.IsPublished.Equals(false))
                throw new TemplateNotPublished();

            if (!sudo && await _store.AtTemplateLimit(newlink.WorkspaceId))
                throw new TemplateLimitReached();

            var workspace = await _store.DbContext.Workspaces
                .FirstOrDefaultAsync(w => w.Id == newlink.WorkspaceId)
            ;

            string name = entity.Name.Length > 60
                ? entity.Name.Substring(0, 60)
                : entity.Name
            ;

            var newTemplate = new Data.Template
            {
                ParentId = entity.Id,
                WorkspaceId = workspace.Id,
                Name = $"{name}-{new Random().Next(100, 999).ToString()}",
                Description = entity.Description,
                Iso = entity.Iso,
                Networks = entity.Networks,
                Guestinfo = entity.Guestinfo
            };

            await _store.Create(newTemplate);

            return Mapper.Map<Template>(
                await _store.Load(newTemplate.Id)
            );
        }

        public async Task<Template> Unlink(TemplateLink link) //CLONE
        {
            var entity = await _store.LoadWithParent(link.TemplateId);

            if (entity.IsLinked)
            {
                TemplateUtility tu = new TemplateUtility(entity.Parent.Detail);

                tu.Name = entity.Name;

                tu.LocalizeDiskPaths(entity.Workspace.Id, entity.Id);

                entity.Detail = tu.ToString();

                entity.Parent = null;

                await _store.Update(entity);
            }

            return Mapper.Map<Template>(
                await _store.Load(link.TemplateId)
            );
        }

        public async Task<Template> Delete(string id)
        {
            var entity = await _store.Retrieve(id);

            if (await _store.HasDescendents(id))
                throw new TemplateHasDescendents();

            // delete associated vm
            var deployable = await GetDeployableTemplate(id);

            await _pod.DeleteAll($"{deployable.Name}#{deployable.IsolationTag}");

            // if root template, delete disk(s)
            if (entity.IsLinked.Equals(false))
                await _pod.DeleteDisks(deployable);

            await _store.Delete(id);

            return Mapper.Map<Template>(entity);
        }

        public async Task<VmTemplate> GetDeployableTemplate(string id, string tag = "")
        {
            var entity = await _store.Load(id);

            return Mapper.Map<ConvergedTemplate>(entity)
                .ToVirtualTemplate()
                .SetHostAffinity(entity.Workspace?.HostAffinity ?? false)
            ;
        }

        public async Task<Dictionary<string, string>> ResolveKeys(string[] keys)
        {
            var map = new Dictionary<string, string>();

            foreach (string key in keys.Distinct())
            {
                var val = await _store.ResolveKey(key);

                map.Add(key, $"{val ?? "__orphaned"}#{key}");
            }

            return map;
        }

        public async Task<string[]> DiskReport()
        {
            var list =  await _store.List().ToArrayAsync();

            return list
                .SelectMany(t =>
                    new TemplateUtility(t.Detail, "").AsTemplate().Disks
                )
                .Select(d => d.Path.Replace("[ds] ", ""))
                .Distinct()
                .OrderBy(x => x)
                .ToArray()
            ;
        }

        /// <summary>
        /// Check health by hitting database and hypervisor
        /// </summary>
        /// <param name="id">Template Id</param>
        /// <returns></returns>
        public async Task CheckHealth(string id)
        {
            var template = await GetDeployableTemplate(id);
            var vm = await _pod.Refresh(template);
            if (vm.Status == "created") // healthy is 'initialized' for existing template
                throw new Exception("bad health");
        }
    }
}
