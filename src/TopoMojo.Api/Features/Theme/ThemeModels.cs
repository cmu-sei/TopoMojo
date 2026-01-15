// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Features.Theme;

public class ThemeInfo
{
    public string? BackgroundUrl { get; set; }
}

public static class ThemeBackground
{
    public static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public static string GetContentType(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}

