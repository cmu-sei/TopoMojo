// Copyright 2026 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace TopoMojo.Hypervisor.Tests;

public sealed class VmConsoleTests
{
    [Theory]
    [InlineData(VmPowerState.Running, true, "running")]
    [InlineData(null, false, null)]
    public void RunningIsDerivedAndBothFieldsRemainInJson(VmPowerState? state, bool running, string stateName)
    {
        var console = new VmConsole { State = state };
        Assert.Equal(running, console.IsRunning);

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(console, options));
        Assert.Equal(running, json.RootElement.GetProperty("isRunning").GetBoolean());
        Assert.Equal(stateName, json.RootElement.GetProperty("state").GetString());
    }
}
