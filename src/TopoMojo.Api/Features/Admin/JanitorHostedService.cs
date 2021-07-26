// Copyright 2021 Carnegie Mellon University.
// Released under a MIT (SEI) license. See LICENSE.md in the project root.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Services;

namespace TopoMojo.HostedServices
{
    public class JanitorHostedService : IHostedService
    {
        private Timer _timer;
        private readonly ILogger _logger;
        private readonly IServiceProvider _services;
        private int periodCount = 0;
        private int periodMax = 100;

        public JanitorHostedService(
            IServiceProvider serviceProvider,
            ILogger<JanitorHostedService> logger
        )
        {
            _services = serviceProvider;
            _logger = logger;
        }

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
            using (var scope = _services.CreateScope())
            {
                var janitor = scope.ServiceProvider.GetService<JanitorService>();

                if (periodCount == 0)
                    _logger.LogInformation("Janitor is checking for stale spaces");

                // run every period
                janitor.EndExpired().Wait();

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
}
