// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using TopoMojo.Api.Models;

namespace TopoMojo.Api
{
    public class DispatchProfile : Profile
    {
        public DispatchProfile()
        {
            CreateMap<Data.Dispatch, Dispatch>()
            ;

            CreateMap<ChangedDispatch, Data.Dispatch>()
            ;

            CreateMap<NewDispatch, Data.Dispatch>()
            ;

            CreateMap<Data.Dispatch, Data.Dispatch>()
                .ForMember(d => d.Id, opt => opt.Ignore())
            ;
        }
    }
}
