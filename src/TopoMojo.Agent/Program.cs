namespace TopoMojo.Agent;

class Program
{
    static async Task Main(string[] args)
    {
        var argv = args.ToList<string>();
        argv.AddRange(new string[] { "", "", "", "" });

        if (argv.Last() == "-h" || argv.Last().Contains("-help"))
        {
            Usage();
            return;
        }

        EventHandlerConfiguration config = new();

        config.Url = !string.IsNullOrEmpty(argv[0]) ? argv[0] : Environment.GetEnvironmentVariable("DISPATCH_URL") ?? "";
        config.ApiKey = !string.IsNullOrEmpty(argv[1]) ? argv[1] : Environment.GetEnvironmentVariable("DISPATCH_APIKEY") ?? "";
        config.GroupId = !string.IsNullOrEmpty(argv[2]) ? argv[2] : Environment.GetEnvironmentVariable("DISPATCH_GROUPID") ?? "";
        config.Hostname = !string.IsNullOrEmpty(argv[3]) ? argv[3] : Environment.GetEnvironmentVariable("HOSTNAME") ?? "";

        if (!config.IsValid)
        {
            Console.WriteLine("You must specify a valid url, apikey, and target group.");
            Usage();
            return;
        }
        EventHandler handler = new(config);

        await handler.Connect();

        Console.WriteLine("TopoMojo Agent running.");

        while (true)
        {
            await Task.Delay(50);
            // Thread.Sleep(50);
        }
    }

    static void Usage()
    {
        Console.WriteLine("usage: TopoMojo.Agent url apikey groupid hostname");
        Console.WriteLine("\tOr supply env vars: DISPATCH_URL, DISPATCH_APIKEY, DISPATCH_GROUPID, HOSTNAME");
    }
}
