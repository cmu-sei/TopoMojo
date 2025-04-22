// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Corsinvest.ProxmoxVE.Api;
using Corsinvest.ProxmoxVE.Api.Shared.Models.Node;
using Microsoft.Extensions.DependencyInjection;
using TopoMojo.Hypervisor.Exceptions;

namespace TopoMojo.Hypervisor.Proxmox
{
    public static class ProxmoxExtensions
    {
        public static long GetId(this Vm vm)
        {
            return long.Parse(vm.Id);
        }

        public static string GetParentFilename(this NodeStorageContent content)
        {
            if (string.IsNullOrEmpty(content.Parent))
            {
                return null;
            }

            // raw format parent: base-<vmId>-disk-<disk_num>@__base__
            if (content.Parent.Contains('@'))
            {
                // return e.g. base-100-disk-0
                return content.Parent.Split('@')[0];
            }
            // qcow2 format: ../<vmId>/base-<vmId>-disk-<disk_num>.qcow2
            else if (content.Parent.StartsWith("../"))
            {
                // return e.g. 100/base-100-disk0.qcow2
                return content.Parent.Split(['/'], 2)[1];
            }
            else
            {
                throw new InvalidOperationException("Unsupported NodeStorageContent type");
            }
        }

        /// <summary>
        /// Adds support for the Proxmox hypervisor to Topomojo.
        /// </summary>
        /// <param name="services">The app's service collection.</param>
        /// <param name="random">
        ///     An instance of Random which will be used across the hypervisor's implementation. Where available,
        ///     The thread-safe Random.Shared instance is recommended. If no instance is supplied, a default
        ///     will be created.
        /// </param>
        /// <returns></returns>
        public static IServiceCollection AddProxmoxHypervisor(this IServiceCollection services, HypervisorServiceConfiguration config, Random random = null)
        {
            return services
                .AddSingleton(typeof(ProxmoxHypervisorService), (sp) => ActivatorUtilities.CreateInstance<ProxmoxHypervisorService>(sp, config))
                .AddSingleton<IProxmoxNameService, ProxmoxNameService>()
                .AddSingleton<IProxmoxVlanManager, ProxmoxVlanManager>((sp) => ActivatorUtilities.CreateInstance<ProxmoxVlanManager>(sp, config))
                .AddSingleton<IProxmoxVnetsClient, ProxmoxVnetsClient>((sp) => ActivatorUtilities.CreateInstance<ProxmoxVnetsClient>(sp, config))
                .AddSingleton(_ => random ?? new Random());
        }

        /// <summary>
        /// Wait for task to finish and then checks the final status of the task. Throws an Exception if task ended in error.
        /// </summary>
        /// Modification of the Corsinvest implementation that adds additional check
        /// that the passed in result actually contains a task id to avoid throwing errors.
        /// https://github.com/Corsinvest/cv4pve-api-dotnet/blob/master/src/Corsinvest.ProxmoxVE.Api/PveClientBase.cs
        /// TODO: Make PR to their repo
        /// <param name="client"></param>
        /// <param name="result">The result representing the task to wait for</param>
        /// <param name="wait"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static async Task<bool> WaitForTaskToFinish(this PveClient client, Result result, int wait = 2000, long timeout = 3600 * 1000)
        {
            if (result == null || timeout <= 0) return false;

            if (result.ResponseInError || !result.IsSuccessStatusCode)
            {
                var statusStr = $"\n Status Code: {result.StatusCode}";
                var reasonStr = string.IsNullOrEmpty(result.ReasonPhrase) ? "" : $"\n Reason: {result.ReasonPhrase}";
                var error = result.GetError();
                var errorStr = string.IsNullOrEmpty(error) ? "" : $"\n Error: {error}";

                throw new HypervisorException($"Task failed: {statusStr}{reasonStr}{errorStr}");
            }

            var data = result.ToData() as string;

            if (data is null || !data.StartsWith("UPID:")) return true;

            var finished = await WaitForTaskToFinish(client, data, wait, timeout);

            if (finished)
            {
                var status = await client.GetExitStatusTaskAsync(data);

                if (status != "OK")
                {
                    throw new HypervisorException($"Task failed: {status}");
                }
            }

            return finished;
        }

        /// <summary>
        /// Wait for task to finish.
        /// </summary>
        /// Modification of Corsinvest implementation that swaps Thread.Sleep with Task.Delay
        /// and fixes timeout.
        /// TODO: Make PR to their repo
        /// <param name="client"></param>
        /// <param name="task">Task identifier</param>
        /// <param name="wait">Millisecond wait next check</param>
        /// <param name="timeout">Millisecond timeout</param>
        /// <return></return>
        public static async Task<bool> WaitForTaskToFinish(this PveClient client, string task, int wait = 2000, long timeout = 3600 * 1000)
        {
            var isRunning = true;
            if (wait <= 0) { wait = 500; }
            if (timeout < wait) { timeout = wait + 5000; }

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (isRunning && stopwatch.ElapsedMilliseconds < timeout)
            {
                await Task.Delay(wait);
                isRunning = await client.TaskIsRunningAsync(task);
            }

            stopwatch.Stop();

            //check timeout
            return stopwatch.ElapsedMilliseconds < timeout;
        }
    }
}
