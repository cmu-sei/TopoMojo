// Copyright 2025 Carnegie Mellon University.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root.

using TopoMojo.Api.Services;

namespace TopoMojo.HostedServices
{
    public class JanitorHostedService(
        IServiceProvider serviceProvider,
        ILogger<JanitorHostedService> logger
        ) : IHostedService
    {
        private Timer _timer;
        private readonly ILogger _logger = logger;
        private readonly IServiceProvider _services = serviceProvider;
        private int periodCount = 0;
        private int periodMax = 100;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Randomness here provides some spread in case
            // the app is being run with multiple replicas.

            var rand = new Random();

            _timer = new Timer(StaleCheck,
                null,
                rand.Next(30, 60) * 1000,
                rand.Next(60, 90) * 1000
            );

            periodMax = rand.Next(60, 90);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.Dispose();

            return Task.CompletedTask;
        }

        private void StaleCheck(object state)
        {
            using var scope = _services.CreateScope();
            var janitor = scope.ServiceProvider.GetService<JanitorService>();

            if (periodCount == 0)
                _logger.LogInformation("Janitor is checking for stale spaces");

            // run every period
            janitor.EndExpired().Wait();
            janitor.CleanupEndedGamespaces().Wait();

            // run after multiple periods (intermittently)
            if (periodCount >= periodMax)
            {
                periodCount = 0;
                janitor.Cleanup().Wait();
            }

            periodCount += 1;
        }
    }
}
