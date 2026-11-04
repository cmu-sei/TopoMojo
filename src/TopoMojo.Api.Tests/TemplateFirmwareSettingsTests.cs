// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;
using TopoMojo.Hypervisor.vSphere;
using VimClient;
using Xunit;

namespace TopoMojo.Api.Tests;

// Firmware and nested virtualization are VirtualMachineConfigSpec properties, not guest variables.
// These run the deploy projection (Detail + Guest Settings -> VmTemplate -> vSphere spec) so the
// legacy Guest Settings spellings keep deploying the same way alongside the top-level settings.
public class TemplateFirmwareSettingsTests
{
    private const string LegacyEfi = """{ "GuestSettings": [ { "Key": "firmware", "Value": "efi" } ] }""";
    private const string LegacyVhv = """{ "GuestSettings": [ { "Key": "vhv.enable", "Value": "TRUE" } ] }""";

    [Fact]
    public void LegacyFirmwareGuestSettingInDetail_StillDeploysEfi()
    {
        Assert.Equal("efi", Deploy(LegacyEfi).firmware);
    }

    [Fact]
    public void FirmwareSetting_DeploysEfi()
    {
        Assert.Equal("efi", Deploy("""{ "Firmware": "efi" }""").firmware);
    }

    [Fact]
    public void FirmwareSetting_IsCaseInsensitive()
    {
        Assert.Equal("efi", Deploy("""{ "firmware": "EFI" }""").firmware);
    }

    [Fact]
    public void FirmwareSettingBios_OverridesALegacyEfiGuestSetting()
    {
        var detail = """{ "Firmware": "bios", "GuestSettings": [ { "Key": "firmware", "Value": "efi" } ] }""";

        Assert.Equal("bios", Deploy(detail).firmware);
    }

    [Fact]
    public void NoFirmwareSetting_LeavesTheVSphereDefault()
    {
        Assert.Null(Deploy("{}").firmware);
    }

    [Fact]
    public void UnknownFirmwareValue_LeavesTheVSphereDefault()
    {
        Assert.Null(Deploy("""{ "Firmware": "uefi" }""").firmware);
    }

    // The Guest Settings box prefixes every key with "guestinfo.", so "firmware=efi" typed there has
    // always deployed as BIOS. Honoring it now would switch those templates to EFI, and a disk
    // installed under BIOS won't boot that way.
    [Fact]
    public void FirmwareTypedInTheGuestSettingsBox_StillDeploysTheDefault()
    {
        var spec = Deploy("{}", guestinfo: "firmware=efi");

        Assert.Null(spec.firmware);
        Assert.Contains(spec.extraConfig, o => o.key == "guestinfo.firmware");
    }

    [Fact]
    public void LegacyVhvGuestSettingInDetail_StillEnablesNestedVirtualization()
    {
        var spec = Deploy(LegacyVhv);

        Assert.True(spec.nestedHVEnabled);
        Assert.True(spec.nestedHVEnabledSpecified);
    }

    // vhv.enable is matched by substring, so the Guest Settings box spelling has always worked.
    [Fact]
    public void VhvTypedInTheGuestSettingsBox_StillEnablesNestedVirtualization()
    {
        Assert.True(Deploy("{}", guestinfo: "vhv.enable=true").nestedHVEnabledSpecified);
    }

    [Fact]
    public void NestedVirtualizationSetting_EnablesIt()
    {
        var spec = Deploy("""{ "NestedVirtualization": true }""");

        Assert.True(spec.nestedHVEnabled);
        Assert.True(spec.nestedHVEnabledSpecified);
    }

    [Fact]
    public void NestedVirtualizationSettingFalse_OverridesALegacyVhvGuestSetting()
    {
        var detail = """{ "NestedVirtualization": false, "GuestSettings": [ { "Key": "vhv.enable", "Value": "true" } ] }""";

        Assert.False(Deploy(detail).nestedHVEnabledSpecified);
    }

    // Legacy keys still reach ExtraConfig exactly as before.
    [Fact]
    public void LegacyGuestSettings_StillReachExtraConfig()
    {
        var spec = Deploy("""{ "GuestSettings": [ { "Key": "firmware", "Value": "efi" }, { "Key": "vhv.enable", "Value": "true" } ] }""");

        Assert.Contains(spec.extraConfig, o => o.key == "firmware" && (string)o.value == "efi");
        Assert.Contains(spec.extraConfig, o => o.key == "vhv.enable" && (string)o.value == "true");
    }

    // Unset settings stay out of the stored Detail, so templates that never use them are unchanged
    // and an older API reading the Detail sees nothing new.
    [Fact]
    public void UnsetSettings_AreNotWrittenToDetail()
    {
        using var doc = JsonDocument.Parse(new TemplateUtility("").ToString());

        Assert.False(doc.RootElement.TryGetProperty(nameof(VmTemplate.Firmware), out _));
        Assert.False(doc.RootElement.TryGetProperty(nameof(VmTemplate.NestedVirtualization), out _));
    }

    [Fact]
    public void SetSettings_RoundTripThroughDetail()
    {
        var template = new TemplateUtility("""{ "Firmware": "efi", "NestedVirtualization": true }""").ToString();
        var reloaded = new TemplateUtility(template).AsTemplate();

        Assert.Equal("efi", reloaded.Firmware);
        Assert.True(reloaded.NestedVirtualization);
    }

    private static VirtualMachineConfigSpec Deploy(string detail, string guestinfo = "")
    {
        var template = new ConvergedTemplate
        {
            Id = "t1",
            Name = "win11",
            Detail = OnDefaultTemplate(detail),
            Guestinfo = guestinfo,
            WorkspaceId = "w1",
        }.ToVirtualTemplate("tag", VSphereHypervisorService.GuestSettingSeparatorSet);

        // No distributed switch, so NICs back onto a standard network by name; no portgroup key needed.
        return Transform.TemplateToVmSpec(template, "[ds]", "");
    }

    // A stored Detail always carries Eth, Disks, etc.; layer the test's properties over a new template's.
    private static string OnDefaultTemplate(string detail)
    {
        var merged = JsonNode.Parse(new TemplateUtility("", "win11").ToString())!.AsObject();

        foreach (var (key, value) in JsonNode.Parse(detail)!.AsObject())
            merged[key] = value?.DeepClone();

        return merged.ToJsonString();
    }
}
