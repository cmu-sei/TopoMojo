// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Text;
using TopoMojo.Hypervisor.Proxmox;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public class ProxmoxFwCfgTests
{
    // Values that previously broke, plus edge cases around the two escapes.
    // The expected round-trip results were produced by driving this escaping through the real
    // Proxmox parser (perl Text::ParseWords::shellwords, which PVE::Tools::split_args uses) and a
    // QemuOpts comma reducer, so ParseArgs below is checked against known-good output.
    public static TheoryData<string> RoundTripValues =>
    [
        "dhcp-range=10.7.42.50,10.7.42.150,12h",
        @"C:\Users\bob",
        "he said \"hi\"",
        "10.7.42.11 web",
        "hosts=a;b",
        "it's a,b",
        "trailing,",
        ",,already doubled",
        "plain",
    ];

    [Theory]
    [MemberData(nameof(RoundTripValues))]
    public void Arg_DeliversTheValueToTheGuestUnchanged(string value)
    {
        var parsed = ParseArgs(ProxmoxFwCfg.Arg("guestinfo.setting", value));

        var fwCfg = Assert.Single(parsed);
        Assert.Equal("opt/guestinfo.setting", fwCfg["name"]);
        Assert.Equal(value, fwCfg["string"]);
    }

    [Theory]
    [MemberData(nameof(RoundTripValues))]
    public void Arg_DeliversTheKeyToTheGuestUnchanged(string key)
    {
        var parsed = ParseArgs(ProxmoxFwCfg.Arg(key, "value"));

        var fwCfg = Assert.Single(parsed);
        Assert.Equal($"opt/{key}", fwCfg["name"]);
        Assert.Equal("value", fwCfg["string"]);
    }

    [Fact]
    public void Arg_DoublesCommasAndSingleQuotesTheWholeArgument()
    {
        // Quoting cannot protect a comma, because Proxmox strips quotes before QemuOpts splits on
        // commas. Both escapes are applied, and single quotes are used so backslashes survive.
        Assert.Equal(
            @"-fw_cfg 'name=opt/guestinfo.dhcp,string=10.7.42.50,,10.7.42.150,,12h'",
            ProxmoxFwCfg.Arg("guestinfo.dhcp", "10.7.42.50,10.7.42.150,12h"));

        Assert.Equal(
            @"-fw_cfg 'name=opt/guestinfo.p,string=C:\Users\bob'",
            ProxmoxFwCfg.Arg("guestinfo.p", @"C:\Users\bob"));

        Assert.Equal(
            @"-fw_cfg 'name=opt/guestinfo.q,string=it'\''s'",
            ProxmoxFwCfg.Arg("guestinfo.q", "it's"));
    }

    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    public void Arg_FoldsNewlinesThatWouldCorruptTheProxmoxConfigFile(string value)
    {
        var arg = ProxmoxFwCfg.Arg("guestinfo.setting", value);

        Assert.DoesNotContain('\n', arg);
        Assert.DoesNotContain('\r', arg);
        Assert.Equal("a b", Assert.Single(ParseArgs(arg))["string"]);
    }

    [Fact]
    public void Arg_TreatsANullValueAsEmpty()
    {
        Assert.Equal(
            "-fw_cfg 'name=opt/guestinfo.setting,string='",
            ProxmoxFwCfg.Arg("guestinfo.setting", null));
    }

    [Fact]
    public void GetArgs_ReturnsNullWhenThereAreNoGuestSettings()
    {
        Assert.Null(ProxmoxClient.GetArgs(new VmTemplate { GuestSettings = null }));
        Assert.Null(ProxmoxClient.GetArgs(new VmTemplate { GuestSettings = [] }));
    }

    [Fact]
    public void GetArgs_EmitsTheDefaultSettingsAndEscapesEachSetting()
    {
        var template = new VmTemplate
        {
            Name = "web#abc123",
            Id = "template-id",
            IsolationTag = "abc123",
            GuestSettings =
            [
                new VmKeyValue { Key = "guestinfo.dhcp", Value = "10.7.42.50,10.7.42.150,12h" },
                new VmKeyValue { Key = "guestinfo.hosts", Value = "10.7.42.11 web" },
            ]
        };

        var parsed = ParseArgs(ProxmoxClient.GetArgs(template));

        Assert.Equal(5, parsed.Count);
        Assert.Equal("abc123", Find(parsed, "opt/guestinfo.isolationTag"));
        Assert.Equal("template-id", Find(parsed, "opt/guestinfo.templateSource"));
        Assert.Equal("web", Find(parsed, "opt/guestinfo.hostname"));
        Assert.Equal("10.7.42.50,10.7.42.150,12h", Find(parsed, "opt/guestinfo.dhcp"));
        Assert.Equal("10.7.42.11 web", Find(parsed, "opt/guestinfo.hosts"));
    }

    [Fact]
    public void GetArgs_StillSkipsIftagSettingsForOtherIsolations()
    {
        var template = new VmTemplate
        {
            Name = "web#abc123",
            Id = "template-id",
            IsolationTag = "abc123",
            GuestSettings =
            [
                new VmKeyValue { Key = "iftag.mine", Value = "abc123:on" },
                new VmKeyValue { Key = "iftag.theirs", Value = "def456:on" },
            ]
        };

        var parsed = ParseArgs(ProxmoxClient.GetArgs(template));

        Assert.Equal("abc123:on", Find(parsed, "opt/iftag.mine"));
        Assert.DoesNotContain(parsed, x => x["name"] == "opt/iftag.theirs");
    }

    private static string Find(List<Dictionary<string, string>> parsed, string name)
        => parsed.Single(x => x["name"] == name)["string"];

    /// <summary>
    /// Replays the two parsers an <c>args</c> string traverses on its way to the guest, so a test
    /// can assert on what the guest actually reads rather than on the escaping itself.
    /// </summary>
    private static List<Dictionary<string, string>> ParseArgs(string args)
    {
        var argv = Shellwords(args);
        var result = new List<Dictionary<string, string>>();

        for (int i = 0; i < argv.Count; i++)
        {
            if (argv[i] != "-fw_cfg")
                continue;

            Assert.True(i + 1 < argv.Count, "-fw_cfg with no argument");
            result.Add(QemuOpts(argv[++i]));
        }

        return result;
    }

    /// <summary>
    /// Mirrors Text::ParseWords::shellwords, which PVE::Tools::split_args uses to turn the vm's
    /// <c>args</c> property into argv. Quotes group and are then stripped; single quotes are
    /// literal, while a backslash escapes the next character outside of them.
    /// </summary>
    private static List<string> Shellwords(string input)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        bool started = false;
        char quote = '\0';

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (quote == '\'')
            {
                if (c == '\'')
                    quote = '\0';
                else
                    current.Append(c);
            }
            else if (quote == '"')
            {
                if (c == '"')
                    quote = '\0';
                else if (c == '\\' && i + 1 < input.Length)
                    current.Append(input[++i]);
                else
                    current.Append(c);
            }
            else if (c is '\'' or '"')
            {
                quote = c;
                started = true;
            }
            else if (c == '\\' && i + 1 < input.Length)
            {
                current.Append(input[++i]);
                started = true;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (started)
                    words.Add(current.ToString());

                current.Clear();
                started = false;
            }
            else
            {
                current.Append(c);
                started = true;
            }
        }

        Assert.Equal('\0', quote); // an unbalanced quote makes shellwords drop everything

        if (started)
            words.Add(current.ToString());

        return words;
    }

    /// <summary>
    /// Mirrors QEMU's QemuOpts parser: split one argv element on single commas, collapsing a
    /// doubled comma to a literal one, then split each token on its first '='.
    /// </summary>
    private static Dictionary<string, string> QemuOpts(string element)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        for (int i = 0; i < element.Length; i++)
        {
            if (element[i] != ',')
            {
                current.Append(element[i]);
            }
            else if (i + 1 < element.Length && element[i + 1] == ',')
            {
                current.Append(',');
                i++;
            }
            else
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        tokens.Add(current.ToString());

        var options = new Dictionary<string, string>();

        foreach (var token in tokens)
        {
            int split = token.IndexOf('=');
            Assert.True(split > 0, $"QemuOpts would reject '{token}' as an invalid parameter");
            options[token[..split]] = token[(split + 1)..];
        }

        return options;
    }
}
