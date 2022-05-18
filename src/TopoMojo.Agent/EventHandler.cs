using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using TopoMojo.Api.Client;

namespace TopoMojo.Agent;

public class EventHandler
{
    EventHandlerConfiguration Config { get; }
    TopoMojoApiClient Mojo { get; }
    HubConnection Hub { get; }

    public EventHandler(
        EventHandlerConfiguration config
    )
    {
        Config = config;

        // set up api client
        HttpClient http = new();
        http.BaseAddress = new Uri(config.Url);
        http.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
        Mojo = new(http);

        // set up hub client
        Hub = new HubConnectionBuilder()
            .WithUrl(
                $"{config.Url}/hub",
                Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets,
                opt =>
                {
                    opt.SkipNegotiation = true;
                    opt.AccessTokenProvider = async () => await GetAuthTicket();
                }
            )
            .WithAutomaticReconnect()
            .Build();

        Hub.On<DispatchEvent>("DispatchEvent", async (DispatchEvent message) => await onMessage(message));

        Hub.Reconnected += async (string? id) =>
        {
            await Hub.InvokeAsync("Listen", config.GroupId);
        };

        Hub.Closed += async (error) =>
        {
            await Task.Delay(new Random().Next(0, 5) * 1000);
            await Hub.StartAsync();
        };

    }

    private async Task onMessage(DispatchEvent message)
    {
        // ensure targetname matches configured hostname, if specified
        if (
            !string.IsNullOrEmpty(message.Model.TargetName) &&
            message.Model?.TargetName.ToLower() != Config.Hostname.ToLower()
        )
        {
            return;
        }

        switch (message.Action)
        {
            case "DISPATCH.CREATE":
                await ProcessMessage(message);
                break;
        }
    }

    private async Task ProcessMessage(DispatchEvent message)
    {
        var args = message.Model.Trigger.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (args.Length == 0)
            return;

        if (string.IsNullOrEmpty(message.Model.TargetName))
            message.Model.TargetName = Config.Hostname;

        try
        {

            ProcessStartInfo info = new()
            {
                FileName = args[0],
                Arguments = string.Join(' ', args.Skip(1)),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            Process? p = Process.Start(info);

            if (p is Process)
            {
                await p.WaitForExitAsync();
                message.Model.Result = await p.StandardOutput.ReadToEndAsync();
                message.Model.Error = await p.StandardError.ReadToEndAsync();
            }
            else
            {
                message.Model.Error = "Agent failed to start process.";
            }
        }
        catch (Exception ex)
        {
            message.Model.Error = ex.Message;
        }

        ChangedDispatch? model = JsonSerializer.Deserialize<ChangedDispatch>(
            JsonSerializer.Serialize<Dispatch>(message.Model)
        );

        await Mojo.UpdateDispatchAsync(model);
    }

    private Task<string> GetAuthTicket()
    {
        var result = Mojo.GetOneTimeTicketAsync().Result;
        return Task.FromResult(result?.Ticket ?? "");
    }

    internal async Task Connect()
    {
        bool connected = false;

        while (!connected)
        {
            try
            {
                await Hub.StartAsync();
                await Hub.InvokeAsync("Listen", Config.GroupId);
                connected = true;
            }
            catch (Exception ex)
            {
                await Task.Delay(1000);
                Console.WriteLine("trying to start websocket... " + ex.Message);
            }
        }
    }

}

public class Actor
{
    public string? Id { get; set; }
    public string? Name { get; set; }
}

public class DispatchEvent
{
    public string Action { get; set; } = "";
    public Actor Actor { get; set; } = new Actor();
    public Dispatch Model { get; set; } = new Dispatch();
}
