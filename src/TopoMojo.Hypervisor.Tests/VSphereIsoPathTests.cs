// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Net.Http;
using Microsoft.Extensions.Logging;
using TopoMojo.Hypervisor;
using TopoMojo.Hypervisor.vSphere;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class VSphereIsoPathTests
{
    private const string WorkspaceId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void GetIsoPaths_UseAScopeFolderPerWorkspace()
    {
        var svc = new VSphereHypervisorService(
            new HypervisorServiceConfiguration { IsoStore = "[datastore] iso/" },
            new LoggerFactory(),
            new StubHttpClientFactory());

        Assert.Equal(
            "0123456789abcdef0123456789abcdef/MyFile.iso",
            svc.GetIsoStorePath(WorkspaceId, "My File.iso"));
        Assert.Equal(
            new[] { "0123456789abcdef0123456789abcdef/MyFile.iso" },
            svc.GetIsoStorePathCandidates(WorkspaceId, "My File.iso"));
        Assert.Equal(
            "[datastore] iso/0123456789abcdef0123456789abcdef/MyFile.iso",
            svc.GetIsoDatastorePath(WorkspaceId, "My File.iso"));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
