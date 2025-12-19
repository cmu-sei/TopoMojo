using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TopoMojo.Api.Features.Theme;

namespace TopoMojo.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class ThemeController(IWebHostEnvironment env) : ControllerBase
{
    private static readonly string[] AllowedExts = [".png", ".jpg", ".jpeg", ".webp"];

    [HttpGet("api/theme")]
    public ActionResult<ThemeInfo> GetTheme()
    {
        var path = FindBackgroundPath();
        if (path is null) return Ok(new ThemeInfo { BackgroundUrl = null });

        var ticks = System.IO.File.GetLastWriteTimeUtc(path).Ticks;
        var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/theme/background?v={ticks}";
        return Ok(new ThemeInfo { BackgroundUrl = url });
    }

    [HttpGet("api/theme/background")]
    public ActionResult GetBackground()
    {
        var path = FindBackgroundPath();
        if (path is null) return NotFound();

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        return PhysicalFile(path, contentType);
    }

    private string ThemeDir()
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "theme");
    }

    private string? FindBackgroundPath()
    {
        var dir = ThemeDir();
        if (!Directory.Exists(dir)) return null;

        foreach (var ext in AllowedExts)
        {
            var candidate = Path.Combine(dir, "background" + ext);
            if (System.IO.File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
