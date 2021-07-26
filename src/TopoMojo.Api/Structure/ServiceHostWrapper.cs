// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace TopoMojo.Api.Services
{
    public class ServiceHostWrapper<T> : IHostedService
        where T : class
    {
        private readonly T backgroundService;

        public ServiceHostWrapper(T backgroundService)
        {
            this.backgroundService = backgroundService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return backgroundService is IHostedService
                ? ((IHostedService)backgroundService).StartAsync(cancellationToken)
                : Task.FromResult(0);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return backgroundService is IHostedService
                ? ((IHostedService)backgroundService).StopAsync(cancellationToken)
                : Task.FromResult(0);
        }
    }
}
