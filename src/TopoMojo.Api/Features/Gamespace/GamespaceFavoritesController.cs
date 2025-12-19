using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Data;
using TopoMojo.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace TopoMojo.Api.Controllers;

[Authorize]
[ApiController]
public class GamespaceFavoritesController(
    ILogger<GamespaceFavoritesController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    TopoMojoDbContext db
) : BaseController(logger, hub)
{
    [HttpGet("api/gamespace-favorites")]
    [SwaggerOperation(OperationId = "ListGamespaceFavorites")]
    public async Task<ActionResult<string[]>> List()
    {
        var ids = await db.GamespaceFavorites
            .Where(x => x.UserId == Actor.Id)
            .Select(x => x.GamespaceId)
            .ToArrayAsync();

        return Ok(ids);
    }

    [HttpPut("api/gamespace-favorite/{gamespaceId}")]
    [SwaggerOperation(OperationId = "FavoriteGamespace")]
    public async Task<IActionResult> Favorite(string gamespaceId)
    {
        var exists = await db.Gamespaces.AnyAsync(g => g.Id == gamespaceId);
        if (!exists) return NotFound();

        var already = await db.GamespaceFavorites.AnyAsync(x =>
            x.UserId == Actor.Id && x.GamespaceId == gamespaceId
        );

        if (!already)
        {
            db.GamespaceFavorites.Add(new GamespaceFavorite
            {
                UserId = Actor.Id,
                GamespaceId = gamespaceId
            });
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpDelete("api/gamespace-favorite/{gamespaceId}")]
    [SwaggerOperation(OperationId = "UnfavoriteGamespace")]
    public async Task<IActionResult> Unfavorite(string gamespaceId)
    {
        var fav = await db.GamespaceFavorites.FirstOrDefaultAsync(x =>
            x.UserId == Actor.Id && x.GamespaceId == gamespaceId
        );

        if (fav != null)
        {
            db.GamespaceFavorites.Remove(fav);
            await db.SaveChangesAsync();
        }

        return Ok();
    }
}
