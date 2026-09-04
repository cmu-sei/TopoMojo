// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
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
        /// Characters that separate one Guest Setting from the next in a template's Guest Settings
        /// text. Every hypervisor separates on newlines; some also accept another character. A
        /// separator cannot appear literally in a value, so each hypervisor declares only what its
        /// own authoring syntax supports.
        /// </summary>
        IReadOnlyList<char> GuestSettingSeparators { get; }
        /// <summary>
        /// Path, relative to the ISO store root, where an ISO for the given scope must be written.
        /// Stores with per-scope folders return "{scopeId}/{fileName}"; flat stores return a single
        /// filename with the scope encoded into it. The hypervisor owns storage layout and filename
        /// semantics.
        /// </summary>
        string GetIsoStorePath(string scopeId, string fileName);
        /// <summary>
        /// Every relative path under the ISO store root at which an ISO for this scope may already
        /// exist, current naming first, followed by superseded namings. Readers probe in order.
        /// </summary>
        IReadOnlyList<string> GetIsoStorePathCandidates(string scopeId, string fileName);
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
