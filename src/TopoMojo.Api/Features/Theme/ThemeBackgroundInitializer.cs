// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using Microsoft.Extensions.Hosting;

namespace TopoMojo.Api.Features.Theme;

public class ThemeBackgroundInitializer : IHostedService
{
    private static readonly string[] AllowedExts = [".png", ".jpg", ".jpeg", ".webp"];
    private readonly IWebHostEnvironment _env;
    private readonly AppSettings _settings;

    public ThemeBackgroundInitializer(IWebHostEnvironment env, AppSettings settings)
    {
        _env = env;
        _settings = settings;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Only act if the operator explicitly set the value
        var configured = _settings.Ui?.Branding?.BackgroundImageUrl;
        if (string.IsNullOrWhiteSpace(configured))
            return Task.CompletedTask;

        var themeDir = ThemeDir();
        Directory.CreateDirectory(themeDir);

        // If user already uploaded a background, don't override it
        if (ExistingBackgroundPath(themeDir) is not null)
            return Task.CompletedTask;

        var src = ResolveConfiguredThemeFile(configured, themeDir);
        if (src is null)
            return Task.CompletedTask;

        var ext = Path.GetExtension(src).ToLowerInvariant();
        var dest = Path.Combine(themeDir, "background" + ext);

        File.Copy(src, dest, overwrite: true);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private string ThemeDir()
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "theme");
    }

    private static string? ExistingBackgroundPath(string themeDir)
    {
        foreach (var ext in AllowedExts)
        {
            var candidate = Path.Combine(themeDir, "background" + ext);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private static string? ResolveConfiguredThemeFile(string configured, string themeDir)
    {
        var s = configured.Trim();

        // Only support local theme files for now (not http(s) URLs)
        if (Uri.TryCreate(s, UriKind.Absolute, out _))
            return null;

        // Accept: "theme/foo.png" or "/theme/foo.png" or "foo.png"
        s = s.TrimStart('/');
        if (s.StartsWith("theme/", StringComparison.OrdinalIgnoreCase))
            s = s.Substring("theme/".Length);

        if (string.IsNullOrWhiteSpace(s) || s.Contains(".."))
            return null;

        var ext = Path.GetExtension(s).ToLowerInvariant();
        if (!AllowedExts.Contains(ext))
            return null;

        var full = Path.GetFullPath(Path.Combine(themeDir, s));
        var root = Path.GetFullPath(themeDir) + Path.DirectorySeparatorChar;

        // Prevent escaping theme dir
        if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return null;

        return File.Exists(full) ? full : null;
    }
}
