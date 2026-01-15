using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using TopoMojo.Api.Features.Theme;

namespace TopoMojo.Api.Controllers;

[ApiController]
[AllowAnonymous]
public class ThemeController(IWebHostEnvironment env, AppSettings settings) : ControllerBase
{
    private static readonly string[] AllowedExts = [".png", ".jpg", ".jpeg", ".webp"];

    [HttpGet("api/theme")]
    public ActionResult<ThemeInfo> GetTheme()
    {
        var configuredRel = ResolveConfiguredThemeRelativePath();
        if (configuredRel is not null)
        {
            var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/{configuredRel}";
            return Ok(new ThemeInfo { BackgroundUrl = url });
        }

        var uploadedPath = FindUploadedBackgroundPath();
        if (uploadedPath is not null)
        {
            var url = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/theme/background";
            return Ok(new ThemeInfo { BackgroundUrl = url });
        }

        return Ok(new ThemeInfo { BackgroundUrl = null });
    }

    [HttpGet("api/theme/background")]
    public ActionResult GetBackground()
    {
        var path = FindUploadedBackgroundPath();
        if (path is null) return NotFound();

        var ext = Path.GetExtension(path).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        // Simple, no ETag: cacheable but always revalidate
        var lastModified = System.IO.File.GetLastWriteTimeUtc(path);

        Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = true,
            MaxAge = TimeSpan.Zero,
            MustRevalidate = true
        };
        Response.GetTypedHeaders().LastModified = lastModified;

        var req = Request.GetTypedHeaders();
        if (req.IfModifiedSince.HasValue)
        {
            var ims = req.IfModifiedSince.Value.UtcDateTime;
            if (lastModified <= ims.AddSeconds(1))
                return StatusCode(StatusCodes.Status304NotModified);
        }

        return PhysicalFile(path, contentType);
    }

    private string ThemeDir()
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "theme");
    }

    private string? FindUploadedBackgroundPath()
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

    private string? ResolveConfiguredThemeRelativePath()
    {
        var configured = settings.Ui?.Branding?.BackgroundImageUrl;
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        var s = configured.Trim();

        // Only support local theme files (not http(s))
        if (Uri.TryCreate(s, UriKind.Absolute, out _))
            return null;

        // Accept: "theme/foo.png" or "/theme/foo.png" or "foo.png"
        s = s.TrimStart('/');
        if (s.StartsWith("theme/", StringComparison.OrdinalIgnoreCase))
            s = s["theme/".Length..];

        if (string.IsNullOrWhiteSpace(s) || s.Contains(".."))
            return null;

        var ext = Path.GetExtension(s).ToLowerInvariant();
        if (!AllowedExts.Contains(ext))
            return null;

        var themeDir = ThemeDir();
        var full = Path.GetFullPath(Path.Combine(themeDir, s));
        var root = Path.GetFullPath(themeDir) + Path.DirectorySeparatorChar;

        // Prevent escaping theme dir
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return null;

        if (!System.IO.File.Exists(full))
            return null;

        // URL path under wwwroot
        return $"theme/{s}";
    }
}
