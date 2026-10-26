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
    public void ListBaseTemplateNames_ListsOnlyTemplatesATemplateCanCloneFrom()
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
            new[] { "kali-rolling-v1", "ubuntu-22.04" },
            ProxmoxClient.ListBaseTemplateNames(cached));
    }
}
