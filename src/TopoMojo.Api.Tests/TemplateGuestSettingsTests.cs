// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using TopoMojo.Hypervisor.Proxmox;
using TopoMojo.Hypervisor.vMock;
using TopoMojo.Hypervisor.vSphere;
using Xunit;

namespace TopoMojo.Api.Tests;

public class TemplateGuestSettingsTests
{
    // The separator sets the hypervisors declare, named here so each test says which syntax it
    // is exercising rather than passing a bare bool.
    private static readonly char[] WithSemicolon = [';', '\n', '\r'];
    private static readonly char[] NewlinesOnly = ['\n', '\r'];

    public static TheoryData<char[]> AllSeparatorSets => [WithSemicolon, NewlinesOnly];

    // Where ';' is not a separator it is ordinary data in a value, so the value must survive whole.
    [Theory]
    [InlineData("hosts=10.7.42.11 web;10.7.42.12 db", "guestinfo.hosts", "10.7.42.11 web;10.7.42.12 db")]
    [InlineData("dhcp=x=1;y=2", "guestinfo.dhcp", "x=1;y=2")]
    public void AddGuestSettings_WhenSemicolonIsNotASeparator_KeepsTheWholeValue(
        string guestinfo, string expectedKey, string expectedValue)
    {
        var settings = Parse(guestinfo, NewlinesOnly);

        var setting = Assert.Single(settings);
        Assert.Equal(expectedKey, setting.Key);
        Assert.Equal(expectedValue, setting.Value);
    }

    [Fact]
    public void AddGuestSettings_WhenSemicolonIsASeparator_SplitsOnIt()
    {
        var settings = Parse("hosts=a;b", WithSemicolon);

        var setting = Assert.Single(settings);
        Assert.Equal("guestinfo.hosts", setting.Key);
        Assert.Equal("a", setting.Value);
    }

    [Fact]
    public void AddGuestSettings_WhenSemicolonIsASeparator_TreatsTheRemainderAsItsOwnSetting()
    {
        var settings = Parse("dhcp=x=1;y=2", WithSemicolon);

        Assert.Equal(2, settings.Length);
        Assert.Equal("x=1", settings.Single(x => x.Key == "guestinfo.dhcp").Value);
        Assert.Equal("2", settings.Single(x => x.Key == "guestinfo.y").Value);
    }

    [Theory]
    [MemberData(nameof(AllSeparatorSets))]
    public void AddGuestSettings_SplitsOnNewlines(char[] separators)
    {
        var settings = Parse("a=1\nb=2\r\nc=3", separators);

        Assert.Equal(3, settings.Length);
        Assert.Equal("1", settings.Single(x => x.Key == "guestinfo.a").Value);
        Assert.Equal("2", settings.Single(x => x.Key == "guestinfo.b").Value);
        Assert.Equal("3", settings.Single(x => x.Key == "guestinfo.c").Value);
    }

    [Theory]
    [MemberData(nameof(AllSeparatorSets))]
    public void AddGuestSettings_SkipsCommentLines(char[] separators)
    {
        var settings = Parse("#comment=ignored\nreal=kept", separators);

        var setting = Assert.Single(settings);
        Assert.Equal("guestinfo.real", setting.Key);
        Assert.Equal("kept", setting.Value);
    }

    [Theory]
    [MemberData(nameof(AllSeparatorSets))]
    public void AddGuestSettings_DoesNotDoublePrefixAnExplicitGuestinfoKey(char[] separators)
    {
        var settings = Parse("guestinfo.already=1", separators);

        Assert.Equal("guestinfo.already", Assert.Single(settings).Key);
    }

    // Pins the per-hypervisor contract: only Proxmox excludes ';', because there a ';' reaches the
    // guest as ordinary value data.
    [Fact]
    public void GuestSettingSeparators_ExcludeSemicolonForProxmoxOnly()
    {
        Assert.DoesNotContain(';', ProxmoxHypervisorService.GuestSettingSeparatorSet);
        Assert.Contains(';', VSphereHypervisorService.GuestSettingSeparatorSet);
        Assert.Contains(';', MockHypervisorService.GuestSettingSeparatorSet);
    }

    [Fact]
    public void GuestSettingSeparators_AlwaysIncludeNewlines()
    {
        char[][] sets =
        [
            ProxmoxHypervisorService.GuestSettingSeparatorSet,
            VSphereHypervisorService.GuestSettingSeparatorSet,
            MockHypervisorService.GuestSettingSeparatorSet,
        ];

        Assert.All(sets, set =>
        {
            Assert.Contains('\n', set);
            Assert.Contains('\r', set);
        });
    }

    // Ties the declared set to the parser, so widening Proxmox's separators fails here and not
    // only in the contract test above.
    [Fact]
    public void AddGuestSettings_WithProxmoxSeparators_KeepsASemicolonInTheValue()
    {
        var settings = Parse("hosts=a;b", ProxmoxHypervisorService.GuestSettingSeparatorSet);

        Assert.Equal("a;b", Assert.Single(settings).Value);
    }

    private static VmKeyValue[] Parse(string guestinfo, IReadOnlyList<char> separators)
    {
        var utility = new TemplateUtility("");
        utility.AddGuestSettings(guestinfo, separators);
        return utility.AsTemplate().GuestSettings;
    }
}
