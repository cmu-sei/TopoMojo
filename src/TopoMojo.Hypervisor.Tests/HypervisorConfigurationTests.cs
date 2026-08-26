using Microsoft.Extensions.Configuration;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class HypervisorConfigurationTests
{
    [Fact]
    public void PodConfiguration_BindsIsoScopeSeparator()
    {
        var configuration = new ConfigurationManager();
        configuration["Pod:IsoScopeSeparator"] = "-";

        var options = configuration
            .GetSection("Pod")
            .Get<HypervisorServiceConfiguration>();

        Assert.Equal("-", options.IsoScopeSeparator);
    }
}
