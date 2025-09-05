// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using TopoMojo.Api.Extensions;
using TopoMojo.Api.Models;

namespace TopoMojo.Api
{
    public class UserProfile : Profile
    {
        public UserProfile()
        {
            CreateMap<Data.User, User>()
                .ForMember(d => d.IsServiceAccount, o => o.MapFrom(s => s.ServiceAccountClientId.NotEmpty()))
                .ReverseMap();

            CreateMap<ChangedUser, Data.User>();

            CreateMap<UserRegistration, Data.User>();

            CreateMap<Data.ApiKey, ApiKey>();
        }
    }
}
