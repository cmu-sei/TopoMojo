// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services
{
    public class JanitorService
    {
        public JanitorService(
            ILogger<JanitorService> logger,
            CoreOptions options,
            IHypervisorService pod,
            IWorkspaceStore workspaceStore,
            IGamespaceStore gamespaceStore
        )
        {
            _logger = logger;
            _options = options;
            _pod = pod;
            _workspaceStore = workspaceStore;
            _gamespaceStore = gamespaceStore;
        }

        private readonly ILogger _logger;
        private readonly CoreOptions _options;
        private readonly IHypervisorService _pod;
        private readonly IWorkspaceStore _workspaceStore;
        private readonly IGamespaceStore _gamespaceStore;

        public async Task EndExpired()
        {
            var ts = DateTimeOffset.UtcNow;

            var unended = await _gamespaceStore.List()
                .Where(g => g.EndTime == DateTimeOffset.MinValue)
                .ToListAsync()
            ;

            var expired = unended
                .Where(g =>
                    g.ExpirationTime.AddMinutes(g.CleanupGraceMinutes) < ts
                )
                .ToArray()
            ;

            foreach (var gs in expired)
            {
                try
                {
                    _logger.LogInformation($"Ending expired gamespace {gs.Id}");

                    gs.EndTime = gs.ExpirationTime;

                    await _gamespaceStore.Update(gs);

                    await _pod.DeleteAll(gs.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to expire gamespace {gs.Id}");
                }
            }
        }

        public async Task<JanitorReport[]> CleanupInactiveWorkspaces(JanitorOptions options)
        {
            return await CleanupWorkspaces(
                "InactiveWorkspace",
                options.InactiveWorkspaceExpiration,
                true,
                options.DryRun
            );
        }

        public async Task<JanitorReport[]> CleanupUnpublishedWorkspaces(JanitorOptions options)
        {
            return await CleanupWorkspaces(
                "UnpublishedWorkspace",
                options.UnpublishedWorkspaceTimeout,
                false,
                options.DryRun
            );
        }

        private async Task<JanitorReport[]> CleanupWorkspaces(
            string reason,
            string expiration,
            bool published,
            bool dryrun
        )
        {
            var items = new List<JanitorReport>();

            var workspaces = (await _workspaceStore.DeleteStale(
                expiration.ToDatePast(),
                published,
                dryrun
            )).ToList();

            if (!dryrun)
            {
                await RemoveVms(workspaces
                    .Select(w => w.Id)
                    .ToArray()
                );
            }

            return workspaces.Select(g => new JanitorReport
            {
                Reason = reason,
                Id = g.Id,
                Name = g.Name,
                Age = g.LastActivity
            }).ToArray();
        }

        public async Task<JanitorReport[]> CleanupIdleWorkspaceVms(JanitorOptions options)
        {
            DateTimeOffset staleDate = options.IdleWorkspaceVmsExpiration.ToDatePast();
            DateTimeOffset activeDate = staleDate.AddSeconds(
                -options.IdleWorkspaceVmsExpiration.ToSeconds()
            );

            var workspaces = await _workspaceStore.List()
                .Where(w =>
                    w.LastActivity > activeDate
                    && w.LastActivity < staleDate
                )
                .ToArrayAsync();

            if (!options.DryRun)
            {
                await RemoveVms(workspaces
                    .Select(w => w.Id)
                    .ToArray()
                );
            }

            return workspaces.Select(g => new JanitorReport
            {
                Reason = "IdleWorkspaceVms",
                Id = g.Id,
                Name = g.Name,
                Age = g.LastActivity
            }).ToArray();
        }

        private async Task RemoveVms(string[] ids)
        {
            var tasks = new List<Task>();

            foreach (var g in ids)
                tasks.Add(_pod.DeleteAll(g));

            await Task.WhenAll(tasks.ToArray());
        }

        public async Task<JanitorReport[]> Cleanup(JanitorOptions options = null)
        {
            var result = new List<JanitorReport>();

            var opt = options ?? _options.Expirations;

            result.AddRange(await CleanupIdleWorkspaceVms(opt));

            result.AddRange(await CleanupUnpublishedWorkspaces(opt));

            result.AddRange(await CleanupInactiveWorkspaces(opt));

            return result.ToArray();
        }

    }

}
