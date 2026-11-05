// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TopoMojo.Api.Controllers;
using Xunit;

namespace TopoMojo.Api.Tests;

/// <summary>
/// Core:EnableTemplateSources turns the admin Disks tab on. It's off by default, and while
/// it's off api/admin/template-sources must not query the hypervisor.
/// </summary>
public sealed class TemplateSourcesSettingTests
{
    // Only the options matter here. The hypervisor and the stores are null, so the test
    // fails with a NullReferenceException if the disabled path ever reaches them.
    private static AdminController Controller(CoreOptions options) => new(
        NullLogger<AdminController>.Instance,
        null, null, null, null, null, null, null,
        podService: null,
        templateStore: null,
        workspaceStore: null,
        coreOptions: options);

    [Fact]
    public void TemplateSourcesAreOffByDefault()
    {
        Assert.False(new CoreOptions().EnableTemplateSources);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FeaturesReportTheSetting(bool enabled)
    {
        var result = Controller(new CoreOptions { EnableTemplateSources = enabled }).GetAdminFeatures();

        var features = Assert.IsType<AdminFeatures>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(enabled, features.TemplateSources);
    }

    [Fact]
    public async Task DisabledTemplateSourcesReturnNothingWithoutAskingTheHypervisor()
    {
        var result = await Controller(new CoreOptions()).GetTemplateSources();

        var report = Assert.IsType<TemplateSourceReport>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(report.Enabled);
        Assert.Null(report.Field);
        Assert.Empty(report.Sources);
    }

    [Fact]
    public void ReportsAreEnabledUnlessTheSettingSaysOtherwise()
    {
        Assert.True(new TemplateSourceReport().Enabled);
    }
}
