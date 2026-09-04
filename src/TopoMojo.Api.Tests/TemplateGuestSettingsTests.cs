// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Linq;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using Xunit;

namespace TopoMojo.Api.Tests;

public class TemplateGuestSettingsTests
{
    // On Proxmox a ';' is ordinary data in a Guest Setting value, so splitting on it truncated the
    // value at the first ';' and leaked the remainder as its own bogus sibling setting.
    [Theory]
    [InlineData("hosts=10.7.42.11 web;10.7.42.12 db", "guestinfo.hosts", "10.7.42.11 web;10.7.42.12 db")]
    [InlineData("dhcp=x=1;y=2", "guestinfo.dhcp", "x=1;y=2")]
    public void AddGuestSettings_WithoutSemicolonSeparator_KeepsTheWholeValue(
        string guestinfo, string expectedKey, string expectedValue)
    {
        var settings = Parse(guestinfo, allowSemicolonSeparator: false);

        var setting = Assert.Single(settings);
        Assert.Equal(expectedKey, setting.Key);
        Assert.Equal(expectedValue, setting.Value);
    }

    // Pins the legacy vSphere behavior, which is unchanged.
    [Fact]
    public void AddGuestSettings_WithSemicolonSeparator_StillSplitsAndDropsTheRemainder()
    {
        var settings = Parse("hosts=a;b", allowSemicolonSeparator: true);

        var setting = Assert.Single(settings);
        Assert.Equal("guestinfo.hosts", setting.Key);
        Assert.Equal("a", setting.Value);
    }

    [Fact]
    public void AddGuestSettings_WithSemicolonSeparator_StillLeaksASiblingKey()
    {
        var settings = Parse("dhcp=x=1;y=2", allowSemicolonSeparator: true);

        Assert.Equal(2, settings.Length);
        Assert.Equal("x=1", settings.Single(x => x.Key == "guestinfo.dhcp").Value);
        Assert.Equal("2", settings.Single(x => x.Key == "guestinfo.y").Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddGuestSettings_SplitsOnNewlinesInEitherMode(bool allowSemicolonSeparator)
    {
        var settings = Parse("a=1\nb=2\r\nc=3", allowSemicolonSeparator);

        Assert.Equal(3, settings.Length);
        Assert.Equal("1", settings.Single(x => x.Key == "guestinfo.a").Value);
        Assert.Equal("2", settings.Single(x => x.Key == "guestinfo.b").Value);
        Assert.Equal("3", settings.Single(x => x.Key == "guestinfo.c").Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddGuestSettings_SkipsCommentLinesInEitherMode(bool allowSemicolonSeparator)
    {
        var settings = Parse("#comment=ignored\nreal=kept", allowSemicolonSeparator);

        var setting = Assert.Single(settings);
        Assert.Equal("guestinfo.real", setting.Key);
        Assert.Equal("kept", setting.Value);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddGuestSettings_DoesNotDoublePrefixAnExplicitGuestinfoKeyInEitherMode(
        bool allowSemicolonSeparator)
    {
        var settings = Parse("guestinfo.already=1", allowSemicolonSeparator);

        Assert.Equal("guestinfo.already", Assert.Single(settings).Key);
    }

    [Theory]
    [InlineData(HypervisorType.Proxmox, false)]
    [InlineData(HypervisorType.Vsphere, true)]
    [InlineData(null, true)]
    public void AllowsSemicolonGuestSettingSeparator_OptsOutForProxmoxOnly(
        HypervisorType? hypervisorType, bool expected)
    {
        Assert.Equal(expected, TemplateExtensions.AllowsSemicolonGuestSettingSeparator(hypervisorType));
    }

    private static VmKeyValue[] Parse(string guestinfo, bool allowSemicolonSeparator)
    {
        var utility = new TemplateUtility("");
        utility.AddGuestSettings(guestinfo, allowSemicolonSeparator);
        return utility.AsTemplate().GuestSettings;
    }
}
