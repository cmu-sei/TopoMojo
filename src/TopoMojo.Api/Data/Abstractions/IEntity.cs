// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

namespace TopoMojo.Api.Data.Abstractions
{
    public interface IEntity
    {
        string Id { get; set; }
        System.DateTimeOffset WhenCreated { get; set; }
    }

}
