// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Services
{
    public class ServiceHostWrapper<T>(T backgroundService) : IHostedService
        where T : class
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return backgroundService is IHostedService service
                ? service.StartAsync(cancellationToken)
                : Task.FromResult(0);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return backgroundService is IHostedService service
                ? service.StopAsync(cancellationToken)
                : Task.FromResult(0);
        }
    }
}
