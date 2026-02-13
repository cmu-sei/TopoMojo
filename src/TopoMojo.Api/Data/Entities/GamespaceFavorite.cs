// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using TopoMojo.Api.Data.Abstractions;

namespace TopoMojo.Api.Data;

public class GamespaceFavorite : IEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string UserId { get; set; } = default!;
    public string GamespaceId { get; set; } = default!;
    public DateTimeOffset WhenCreated { get; set; } = DateTimeOffset.UtcNow;
}
