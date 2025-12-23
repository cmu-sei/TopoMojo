using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using TopoMojo.Api.Data;
using TopoMojo.Api.Hubs;

namespace TopoMojo.Api.Controllers;

[Authorize]
[ApiController]
public class TemplateFavoritesController(
    ILogger<TemplateFavoritesController> logger,
    IHubContext<AppHub, IHubEvent> hub,
    TopoMojoDbContext db
) : BaseController(logger, hub)
{
    [HttpGet("api/template-favorites")]
    [SwaggerOperation(OperationId = "ListTemplateFavorites")]
    public async Task<ActionResult<string[]>> List()
    {
        if (!Actor.IsAdmin) return Forbid();

        var ids = await db.TemplateFavorites
            .Where(x => x.UserId == Actor.Id)
            .Select(x => x.TemplateId)
            .ToArrayAsync();

        return Ok(ids);
    }

    [HttpPut("api/template-favorite/{templateId}")]
    [SwaggerOperation(OperationId = "FavoriteTemplate")]
    public async Task<IActionResult> Favorite(string templateId)
    {
        if (!Actor.IsAdmin) return Forbid();

        var exists = await db.Templates.AnyAsync(t => t.Id == templateId);
        if (!exists) return NotFound();

        var already = await db.TemplateFavorites.AnyAsync(x =>
            x.UserId == Actor.Id && x.TemplateId == templateId
        );

        if (!already)
        {
            db.TemplateFavorites.Add(new TemplateFavorite
            {
                UserId = Actor.Id,
                TemplateId = templateId
            });

            await db.SaveChangesAsync();
        }

        return Ok();
    }

    [HttpDelete("api/template-favorite/{templateId}")]
    [SwaggerOperation(OperationId = "UnfavoriteTemplate")]
    public async Task<IActionResult> Unfavorite(string templateId)
    {
        if (!Actor.IsAdmin) return Forbid();

        var fav = await db.TemplateFavorites.FirstOrDefaultAsync(x =>
            x.UserId == Actor.Id && x.TemplateId == templateId
        );

        if (fav != null)
        {
            db.TemplateFavorites.Remove(fav);
            await db.SaveChangesAsync();
        }

        return Ok();
    }
}
