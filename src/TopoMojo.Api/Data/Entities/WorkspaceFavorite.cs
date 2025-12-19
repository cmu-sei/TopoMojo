using TopoMojo.Api.Data.Abstractions;

namespace TopoMojo.Api.Data;

public class WorkspaceFavorite : IEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = default!;
    public string WorkspaceId { get; set; } = default!;
    public DateTimeOffset WhenCreated { get; set; } = DateTimeOffset.UtcNow;
}
