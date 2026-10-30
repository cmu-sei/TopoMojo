using TopoMojo.Hypervisor;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class ProxmoxDeleteDisksTests
{
    private const string TemplateId = "ae185f2a217249d7ab5843459502d31a";

    [Fact]
    public void OwnsTemplateVm_IsTrueForTheCopyUnlinkingMade()
    {
        // Unlinking sets Template to the template id and moves the base to ParentTemplate.
        var copy = new VmTemplate { Id = TemplateId, Template = TemplateId, ParentTemplate = "Alpine-Disk" };

        Assert.True(ProxmoxHypervisorService.OwnsTemplateVm(copy));
    }

    [Theory]
    // a stock template on a shared base
    [InlineData(TemplateId, "Alpine-Disk")]
    // a clone of a copy names the original's vm
    [InlineData("0123456789abcdef0123456789abcdef", TemplateId)]
    // a blank Template would match any cached vm with no name
    [InlineData(TemplateId, null)]
    [InlineData(TemplateId, "")]
    [InlineData(null, TemplateId)]
    public void OwnsTemplateVm_IsFalseForAVmTheTemplateDoesNotOwn(string id, string template)
    {
        Assert.False(ProxmoxHypervisorService.OwnsTemplateVm(new VmTemplate { Id = id, Template = template }));
    }
}
