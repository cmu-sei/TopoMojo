// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Threading.Tasks;

namespace TopoMojo.Hypervisor
{
    public interface IHypervisorService
    {
        Task<Vm> Load(string id);
        Task<Vm> Start(string id);
        Task<Vm> Stop(string id);
        Task<Vm> Save(string id);
        Task<Vm> Revert(string id);
        Task<Vm> Delete(string id);
        Task StartAll(string target);
        Task StopAll(string target);
        Task DeleteAll(string target);
        Task<Vm> ChangeState(VmOperation op);
        Task<Vm> ChangeConfiguration(string id, VmKeyValue change, bool privileged = false);
        Task<Vm> Deploy(VmTemplate template, bool privileged = false);
        Task Deploy(DeploymentContext ctx, bool wait = false);
        Task SetAffinity(string isolationTag, Vm[] vms, bool start);
        Task<Vm> Refresh(VmTemplate template);
        Task<Vm[]> Find(string searchText);
        Task<int> CreateDisks(VmTemplate template);
        Task<int[]> VerifyDisks(VmTemplate template);
        Task DeleteDisks(VmTemplate template);
        Task<VmConsole> Display(string id);
        Task<Vm> Answer(string id, VmAnswer answer);
        // Task<TemplateOptions> GetTemplateOptions(string key);
        Task<VmOptions> GetVmIsoOptions(string key);
        Task<VmOptions> GetAllIsoOptions();
        Task<VmOptions> GetVmNetOptions(string key);
        string Version { get; }
        Task ReloadHost(string host);
        HypervisorServiceConfiguration Options { get; }
        /// <summary>
        /// True when this hypervisor's file stores keep one folder per workspace or public bin.
        /// False when a store is flat and the scope has to be encoded into the stored filename.
        /// This is a property of the hypervisor's storage, not a deployment choice.
        /// </summary>
        bool SupportsSubfolders { get; }
        /// <summary>
        /// Builds the logical datastore path for an ISO in the given workspace or public bin.
        /// The hypervisor owns storage layout and filename semantics.
        /// </summary>
        string GetIsoDatastorePath(string scopeId, string fileName);

        /// <summary>
        /// Upload a file to datastore via API.
        /// </summary>
        /// <param name="datastorePath">Hypervisor-specific logical datastore path.</param>
        /// <param name="localFilePath">Local filesystem path to file to upload</param>
        /// <returns>The datastore path where file was uploaded</returns>
        Task<string> UploadFileToDatastore(string datastorePath, string localFilePath);

        /// <summary>
        /// Delete a file from datastore via API.
        /// </summary>
        /// <param name="datastorePath">Hypervisor-specific logical datastore path.</param>
        Task DeleteFileFromDatastore(string datastorePath);
    }

}
