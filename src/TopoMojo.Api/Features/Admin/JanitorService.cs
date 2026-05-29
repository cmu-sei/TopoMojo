// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;
using TopoMojo.Hypervisor;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services
{
    public class JanitorService(
        ILogger<JanitorService> logger,
        CoreOptions options,
        IHypervisorService pod,
        IWorkspaceStore workspaceStore,
        IGamespaceStore gamespaceStore,
        FileUploadOptions fileUploadOptions
        )
    {
        private readonly ILogger _logger = logger;
        private readonly CoreOptions _options = options;
        private readonly IHypervisorService _pod = pod;
        private readonly IWorkspaceStore _workspaceStore = workspaceStore;
        private readonly IGamespaceStore _gamespaceStore = gamespaceStore;
        private readonly FileUploadOptions _fileUploadOptions = fileUploadOptions;

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
                    _logger.LogInformation("Cleaning ended gamespace {id}", gs.Id);

                    await _pod.DeleteAll(gs.Id);

                    gs.Cleaned = true;

                    await _gamespaceStore.Update(gs);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to clean gamespace {id}", gs.Id);
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

            // Load full workspace details with workers and templates to get owner and VM count
            var workspaceIds = workspaces.Select(w => w.Id).ToArray();
            var workspacesWithDetails = await _workspaceStore.List()
                .Where(w => workspaceIds.Contains(w.Id))
                .Include(w => w.Workers)
                .Include(w => w.Templates)
                .ToListAsync();

            return workspaces.Select(g =>
            {
                var details = workspacesWithDetails.FirstOrDefault(w => w.Id == g.Id);
                var owner = details?.Workers.FirstOrDefault(w => w.CanManage);

                return new JanitorReport
                {
                    Reason = reason,
                    Id = g.Id,
                    Name = g.Name,
                    Age = g.LastActivity,
                    OwnerName = owner?.SubjectName ?? "Unknown",
                    VmCount = details?.Templates.Count ?? 0
                };
            }).ToArray();
        }

        public async Task<JanitorReport[]> CleanupIdleWorkspaceVms(JanitorOptions options)
        {
            DateTimeOffset keepAliveDate = options.IdleWorkspaceVmsExpiration.ToDatePast();
            DateTimeOffset previousWindow = keepAliveDate.AddSeconds(
                -options.IdleWorkspaceVmsExpiration.ToSeconds()
            );

            var workspaces = await _workspaceStore.List()
                .Include(w => w.Workers)
                .Include(w => w.Templates)
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

            return workspaces.Select(g =>
            {
                var owner = g.Workers.FirstOrDefault(w => w.CanManage);

                return new JanitorReport
                {
                    Reason = "IdleWorkspaceVms",
                    Id = g.Id,
                    Name = g.Name,
                    Age = g.LastActivity,
                    OwnerName = owner?.SubjectName ?? "Unknown",
                    VmCount = g.Templates.Count
                };
            }).ToArray();
        }

        private async Task RemoveVms(string[] ids)
        {
            await Task.WhenAll(
                ids.Select(_pod.DeleteAll)
            );
        }

        public async Task<JanitorReport[]> Cleanup(JanitorOptions options = null)
        {
            var result = new List<JanitorReport>();

            var opt = options ?? _options.Expirations;

            result.AddRange(await CleanupIdleWorkspaceVms(opt));

            result.AddRange(await CleanupUnpublishedWorkspaces(opt));

            result.AddRange(await CleanupInactiveWorkspaces(opt));

            return [.. result];
        }

        public Task CleanupStaleTempFiles()
        {
            if (!_fileUploadOptions.UseDatastoreApi || string.IsNullOrEmpty(_fileUploadOptions.TempRoot))
                return Task.CompletedTask;

            try
            {
                if (!Directory.Exists(_fileUploadOptions.TempRoot))
                    return Task.CompletedTask;

                var cutoff = DateTimeOffset.UtcNow.AddHours(-_fileUploadOptions.TempFileExpirationHours);
                var deletedCount = 0;

                foreach (var file in Directory.GetFiles(_fileUploadOptions.TempRoot, "*.*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTimeUtc < cutoff)
                        {
                            File.Delete(file);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete stale temp file: {file}", file);
                    }
                }

                if (deletedCount > 0)
                    _logger.LogInformation("Cleaned up {count} stale temp files from {path}", deletedCount, _fileUploadOptions.TempRoot);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to cleanup temp files in {path}", _fileUploadOptions.TempRoot);
            }

            return Task.CompletedTask;
        }

    }

}
