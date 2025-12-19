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
public class WorkspaceFavoritesController(
    ILogger<WorkspaceFavoritesController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    TopoMojoDbContext db
) : BaseController(logger, hub)
{
    [HttpGet("api/workspace-favorites")]
    [SwaggerOperation(OperationId = "ListWorkspaceFavorites")]
    public async Task<ActionResult<string[]>> List()
    {
        var ids = await db.WorkspaceFavorites
            .Where(x => x.UserId == Actor.Id)
            .Select(x => x.WorkspaceId)
            .ToArrayAsync();

        return Ok(ids);
    }

    [HttpPut("api/workspace-favorite/{workspaceId}")]
    [SwaggerOperation(OperationId = "FavoriteWorkspace")]
    public async Task<IActionResult> Favorite(string workspaceId)
    {
        var exists = await db.Workspaces.AnyAsync(w => w.Id == workspaceId);
        if (!exists) return NotFound();

        var already = await db.WorkspaceFavorites.AnyAsync(x =>
            x.UserId == Actor.Id && x.WorkspaceId == workspaceId
        );

        if (!already)
        {
            db.WorkspaceFavorites.Add(new WorkspaceFavorite
            {
                UserId = Actor.Id,
                WorkspaceId = workspaceId
            });
            await db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpDelete("api/workspace-favorite/{workspaceId}")]
    [SwaggerOperation(OperationId = "UnfavoriteWorkspace")]
    public async Task<IActionResult> Unfavorite(string workspaceId)
    {
        var fav = await db.WorkspaceFavorites.FirstOrDefaultAsync(x =>
            x.UserId == Actor.Id && x.WorkspaceId == workspaceId
        );

        if (fav != null)
        {
            db.WorkspaceFavorites.Remove(fav);
            await db.SaveChangesAsync();
        }

        return Ok();
    }
}
