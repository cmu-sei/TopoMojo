// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Data.Abstractions;
namespace TopoMojo.Api.Data;

public class GamespaceFavorite
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = default!;
    public string GamespaceId { get; set; } = default!;
}
