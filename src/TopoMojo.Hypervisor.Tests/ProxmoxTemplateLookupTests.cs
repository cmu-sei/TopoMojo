using System;
using System.Collections.Generic;
using System.Linq;
using TopoMojo.Hypervisor.Exceptions;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ProxmoxTemplateLookupTests
{
    [Fact]
    public void SelectTemplateVm_ReturnsMatchingTemplate()
    {
        var cached = new[]
        {
            new Vm { Id = "100", Name = "other-v1" },
            new Vm { Id = "101", Name = "kali-rolling-v1" }
        };

        var vm = ProxmoxClient.SelectTemplateVm(cached, "kali-rolling-v1");

        Assert.Equal("101", vm.Id);
    }

    [Fact]
    public void SelectTemplateVm_SkipsCopiesTaggedForDeletion()
    {
        var cached = new[]
        {
            new Vm { Id = "100", Name = "kali-rolling-v1", Tags = ["delete"] },
            new Vm { Id = "101", Name = "kali-rolling-v1" }
        };

        var vm = ProxmoxClient.SelectTemplateVm(cached, "kali-rolling-v1");

        Assert.Equal("101", vm.Id);
    }

    [Fact]
    public void SelectTemplateVm_NamesTheTemplateWhenTheCacheHasNoMatch()
    {
        // A node with pvestatd stopped returns resources with no name, so its templates
        // never make it into the cache.
        var cached = new[]
        {
            new Vm { Id = "100", Name = "other-v1" },
            new Vm { Id = "101", Name = null }
        };

        var ex = Assert.Throws<HypervisorException>(
            () => ProxmoxClient.SelectTemplateVm(cached, "kali-rolling-v1"));

        Assert.Contains("kali-rolling-v1", ex.Message);
        Assert.Contains("pvestatd", ex.Message);
    }

    [Fact]
    public void SelectTemplateVm_NamesTheTemplateWhenEveryCopyIsTaggedForDeletion()
    {
        var cached = new[]
        {
            new Vm { Id = "100", Name = "kali-rolling-v1", Tags = ["delete"] }
        };

        var ex = Assert.Throws<HypervisorException>(
            () => ProxmoxClient.SelectTemplateVm(cached, "kali-rolling-v1"));

        Assert.Contains("kali-rolling-v1", ex.Message);
        Assert.Contains("delete", ex.Message);
    }

    [Fact]
    public void ListTemplateVms_ListsTemplatesATemplateCanCloneFrom()
    {
        var cached = new[]
        {
            new Vm { Id = "100", Name = "ubuntu-22.04", IsTemplate = true },
            new Vm { Id = "101", Name = "kali-rolling-v1", IsTemplate = true },
            new Vm { Id = "102", Name = "kali-rolling-v1", IsTemplate = true, Tags = ["delete"] },
            new Vm { Id = "103", Name = "old-base", IsTemplate = true, Tags = ["delete"] },
            new Vm { Id = "104", Name = "0123456789abcdef0123456789abcdef", IsTemplate = true },
            new Vm { Id = "105", Name = "player-vm#abc", IsTemplate = false },
            new Vm { Id = "106", Name = null, IsTemplate = true }
        };

        Assert.Equal(
            new[] { "104", "101", "100" },
            ProxmoxClient.ListTemplateVms(cached).Select(x => x.Id));
    }

    [Theory]
    [InlineData("creation-qemu=8.1.2,ctime=1712345678", 1712345678L)]
    [InlineData("ctime=1712345678", 1712345678L)]
    [InlineData("creation-qemu=8.1.2", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ParseMetaCreated_ReadsCtime(string meta, long? expected)
    {
        Assert.Equal(
            expected.HasValue ? DateTimeOffset.FromUnixTimeSeconds(expected.Value) : null,
            ProxmoxClient.ParseMetaCreated(meta));
    }

    [Fact]
    public void ParseDiskVolumes_ListsDiskVolumesButNotCdroms()
    {
        var config = new Dictionary<string, object>
        {
            ["scsi0"] = "local-lvm:base-9002-disk-0,size=8G",
            ["virtio1"] = "local-lvm:base-9002-disk-1,cache=writeback",
            ["efidisk0"] = "local-lvm:base-9002-disk-2,efitype=4m",
            ["ide2"] = "local:iso/alpine.iso,media=cdrom",
            ["sata1"] = "none,media=cdrom",
            ["net0"] = "virtio=BC:24:11:00:00:01,bridge=vmbr0",
            ["meta"] = "creation-qemu=8.1.2,ctime=1712345678"
        };

        Assert.Equal(
            new[] { "local-lvm:base-9002-disk-0", "local-lvm:base-9002-disk-1", "local-lvm:base-9002-disk-2" },
            ProxmoxClient.ParseDiskVolumes(config));
        Assert.Empty(ProxmoxClient.ParseDiskVolumes(null));
    }
}
