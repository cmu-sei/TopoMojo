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
                .Where(g => g.EndTime <= DateTimeOffset.MinValue)
                .ToListAsync()
            ;

            var expired = unended
                .Where(g =>
                    g.ExpirationTime.AddMinutes(g.CleanupGraceMinutes) < ts
                )
                .ToArray()
            ;

            foreach (var gs in expired)
                gs.EndTime = gs.ExpirationTime;

            await _gamespaceStore.Update(expired);
        }

        public async Task CleanupEndedGamespaces()
        {
            var ts = DateTimeOffset.UtcNow;

            var ended = await _gamespaceStore.List()
                .Where(g => g.EndTime > DateTimeOffset.MinValue && !g.Cleaned)
                .ToListAsync()
            ;

            foreach (var gs in ended)
            {
                try
                {
                    _logger.LogInformation($"Cleaning ended gamespace {gs.Id}");

                    await _pod.DeleteAll(gs.Id);

                    gs.Cleaned = true;

                    await _gamespaceStore.Update(gs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to clean gamespace {gs.Id}");
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
            // Force dry-run pending further thought
            dryrun = true;

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
            DateTimeOffset keepAliveDate = options.IdleWorkspaceVmsExpiration.ToDatePast();
            DateTimeOffset previousWindow = keepAliveDate.AddSeconds(
                -options.IdleWorkspaceVmsExpiration.ToSeconds()
            );

            var workspaces = await _workspaceStore.List()
                .Where(w =>
                    w.LastActivity > previousWindow
                    && w.LastActivity < keepAliveDate
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
