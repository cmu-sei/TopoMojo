// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.Extensions.Caching.Memory;
using TopoMojo.Api.Exceptions;
using TopoMojo.Api.Models;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Services
{
    /// <summary>
    /// Hypervisor capacity figures shared by the capacity estimate, the admin dashboard and
    /// the deploy check, read at most once per Core:CapacityCacheSeconds.
    /// </summary>
    public static class HypervisorCapacityCache
    {
        public const string Key = "hypervisor:capacity";

        /// <summary>
        /// A failed read is not cached.
        /// </summary>
        public static Task<HypervisorCapacity> Get(IMemoryCache cache, IHypervisorService pod, CoreOptions options)
        {
            return cache.GetOrCreateAsync(Key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(
                    Math.Max(options.CapacityCacheSeconds, 1)
                );
                return pod.GetCapacity();
            });
        }
    }

    /// <summary>
    /// Checks a deploy against free hypervisor memory before it starts (Core:CapacityCheck).
    /// </summary>
    /// <remarks>
    /// The hypervisor snapshot is cached, and deploys that are still running don't show in
    /// it yet, so every deploy that passes the check holds a reservation for its memory.
    /// A reservation counts against free memory while its deploy runs and until a snapshot
    /// read after the deploy finished, which then shows the vms itself. A failed deploy
    /// gives its reservation back. The check and the reservation are taken together under
    /// one lock, so two deploys can't both take the last room in one instance of the API.
    /// Deploys from other API replicas or other apps on the hypervisor aren't seen until
    /// the next snapshot.
    /// </remarks>
    public class CapacityGuard(
        IHypervisorService podService,
        IMemoryCache localCache,
        CoreOptions options,
        ILogger<CapacityGuard> logger
    )
    {
        // an in-flight reservation whose deploy never reported back is dropped after this
        private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(30);

        private readonly object _sync = new();
        private readonly List<CapacityReservation> _reservations = [];

        /// <summary>
        /// Check a deploy and reserve its memory. Complete the reservation when the deploy
        /// finishes, or release it if the deploy fails.
        /// </summary>
        /// <param name="target">What is deploying, for the log</param>
        /// <param name="demand">Memory of the vms about to deploy</param>
        /// <param name="hostAffinity">All the vms must land on one host</param>
        /// <param name="allowOverride">Deploy with a warning even in Block mode</param>
        /// <exception cref="CapacityExceeded">Block mode and the deploy doesn't fit</exception>
        public async Task<CapacityReservation> Reserve(
            string target,
            WorkspaceDemand demand,
            bool hostAffinity,
            bool allowOverride = false)
        {
            if (options.CapacityCheck == CapacityCheckMode.Off || demand.MemoryMb <= 0)
                return CapacityReservation.None;

            HypervisorCapacity capacity;

            try
            {
                capacity = await HypervisorCapacityCache.Get(localCache, podService, options);
            }
            catch (Exception ex)
            {
                // never refuse a deploy because the hypervisor's figures can't be read
                logger.LogWarning(ex, "Capacity check skipped for {target}: hypervisor capacity could not be read", target);
                return CapacityReservation.None;
            }

            if (capacity is null)
                return CapacityReservation.None;

            lock (_sync)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;

                _reservations.RemoveAll(r =>
                    (r.CompletedAt.HasValue && r.CompletedAt <= capacity.AsOf)
                    || now - r.StartedAt > StaleAfter
                );

                long reservedMb = _reservations.Sum(r => r.MemoryMb);

                var check = CapacityCalculator.Check(
                    demand,
                    capacity,
                    reservedMb,
                    options.CapacityHeadroomPercent,
                    hostAffinity
                );

                string warning = null;

                if (!check.Fits)
                {
                    if (options.CapacityCheck == CapacityCheckMode.Block && !allowOverride)
                    {
                        logger.LogWarning("Capacity check refused deploy of {target}: {message}", target, check.Message);
                        throw new CapacityExceeded(check.Message);
                    }

                    warning = options.CapacityCheck == CapacityCheckMode.Block
                        ? $"{check.Message} Deployed anyway because an administrator started it."
                        : check.Message;

                    logger.LogWarning("Capacity check warning for deploy of {target}: {message}", target, warning);
                }

                var reservation = new CapacityReservation(this, demand.MemoryMb, now, warning);
                _reservations.Add(reservation);
                return reservation;
            }
        }

        /// <summary>
        /// Memory held by reservations a snapshot taken at asOf doesn't show yet.
        /// </summary>
        public long ReservedMb(DateTimeOffset asOf)
        {
            lock (_sync)
            {
                return _reservations
                    .Where(r => !r.CompletedAt.HasValue || r.CompletedAt > asOf)
                    .Sum(r => r.MemoryMb);
            }
        }

        internal void Complete(CapacityReservation reservation)
        {
            lock (_sync)
            {
                reservation.CompletedAt ??= DateTimeOffset.UtcNow;
            }
        }

        internal void Release(CapacityReservation reservation)
        {
            lock (_sync)
            {
                _reservations.Remove(reservation);
            }
        }
    }

    public class CapacityReservation
    {
        public static readonly CapacityReservation None = new(null, 0, DateTimeOffset.MinValue, null);

        private readonly CapacityGuard _guard;

        internal CapacityReservation(CapacityGuard guard, long memoryMb, DateTimeOffset startedAt, string warning)
        {
            _guard = guard;
            MemoryMb = memoryMb;
            StartedAt = startedAt;
            Warning = warning;
        }

        public long MemoryMb { get; }
        public DateTimeOffset StartedAt { get; }
        public DateTimeOffset? CompletedAt { get; internal set; }

        /// <summary>
        /// Why the deploy may not fit, when it went ahead anyway. Null when it fits.
        /// </summary>
        public string Warning { get; }

        /// <summary>
        /// The deploy finished; keep counting it until a newer snapshot shows its vms.
        /// </summary>
        public void Complete() => _guard?.Complete(this);

        /// <summary>
        /// The deploy failed; stop counting it.
        /// </summary>
        public void Release() => _guard?.Release(this);
    }
}
