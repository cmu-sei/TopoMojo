namespace TopoMojo.Agent;

public class EventHandlerConfiguration
{
    public string Url { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string Hostname { get; set; } = "";
    public string HeartbeatTrigger { get; set; } = "";
    public int HeartbeatSeconds { get; set; } = 10;

    public bool IsValid =>
        Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri) &&
        !string.IsNullOrEmpty(ApiKey) &&
        !string.IsNullOrEmpty(GroupId)
    ;
}
