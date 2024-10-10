// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using TopoMojo.Api.Models;

namespace TopoMojo.Api.Services
{
    public class DispatchProfile : Profile
    {
        public DispatchProfile()
        {
            CreateMap<string, string>().ConvertUsing(str => str == null ? null : str.Trim());

            CreateMap<Data.Dispatch, Dispatch>();

            CreateMap<Dispatch, Data.Dispatch>();

            CreateMap<NewDispatch, Data.Dispatch>();

            CreateMap<ChangedDispatch, Data.Dispatch>();
        }
    }
}
