// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Threading.Tasks;

namespace TopoMojo.Api.Validators
{
    public interface IModelValidator
    {
        Task Validate(object model);
    }
}
