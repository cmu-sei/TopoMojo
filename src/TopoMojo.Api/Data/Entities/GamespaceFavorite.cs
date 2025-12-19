using TopoMojo.Api.Data.Abstractions;
namespace TopoMojo.Api.Data;

public class GamespaceFavorite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;
    public string GamespaceId { get; set; } = default!;
}
