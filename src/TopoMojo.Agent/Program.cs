// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Agent;

class Program
{
    internal static readonly string[] arg_padding = ["", "", "", "", "", ""];

    static async Task Main(string[] args)
    {
        var argv = args.ToList();
        argv.AddRange(arg_padding);

        if (argv.Contains("-h") || argv.Contains("--help"))
        {
            Usage();
            return;
        }

        EventHandlerConfiguration config = new()
        {
            Url = !string.IsNullOrEmpty(argv[0]) ? argv[0] : Environment.GetEnvironmentVariable("DISPATCH_URL") ?? "",
            ApiKey = !string.IsNullOrEmpty(argv[1]) ? argv[1] : Environment.GetEnvironmentVariable("DISPATCH_APIKEY") ?? "",
            GroupId = !string.IsNullOrEmpty(argv[2]) ? argv[2] : Environment.GetEnvironmentVariable("DISPATCH_GROUPID") ?? "",
            Hostname = !string.IsNullOrEmpty(argv[3]) ? argv[3] : Environment.GetEnvironmentVariable("HOSTNAME") ?? "",
            HeartbeatTrigger = !string.IsNullOrEmpty(argv[4]) ? argv[4] : Environment.GetEnvironmentVariable("DISPATCH_HEARTBEATTRIGGER") ?? "",
            HeartbeatSeconds = Int32.Parse(
            !string.IsNullOrEmpty(argv[5]) ? argv[5] : Environment.GetEnvironmentVariable("DISPATCH_HEARTBEATSECONDS") ?? "10"
        ),
            QuietLogging = bool.Parse(Environment.GetEnvironmentVariable("DISPATCH_QUIET") ?? "false")
        };

        if (!config.IsValid)
        {
            Console.WriteLine("You must specify a valid url, apikey, and target group.");
            Usage();
            return;
        }
        EventHandler handler = new(config);

        await handler.Connect();

        Console.WriteLine("TopoMojo Agent running.");

        DateTime last_hb = DateTime.Now;

        while (true)
        {
            if (DateTime.Now.Subtract(last_hb).TotalSeconds > config.HeartbeatSeconds)
            {
                await handler.Heartbeat();
                last_hb = DateTime.Now;
            }

            await Task.Delay(100);
        }
    }

    static void Usage()
    {
        Console.WriteLine("usage: TopoMojo.Agent url apikey groupid hostname [heartbeat_trigger, heartbeat_seconds]");
        Console.WriteLine("\tOr supply env vars: DISPATCH_URL, DISPATCH_APIKEY, DISPATCH_GROUPID, DISPATCH_HEARTBEATTRIGGER, DISPATCH_HEATBEATSECONDS, HOSTNAME");
        Console.WriteLine("\tIf you want a heartbeat that doesn't trigger a script, use 'noop' as the trigger.");
    }
}
