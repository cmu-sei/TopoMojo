// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using TopoMojo.Api.Models;
using TopoMojo.Api.Services;

namespace TopoMojo.Api;

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<Data.User, User>()
            .ForMember(d => d.AppRole, o => o.MapFrom(s => s.Role))
            .ForMember(d => d.Role, o => o.MapFrom(s => UserService.ResolveEffectiveRole(s.Role, s.LastIdpAssignedRole)))
            .ReverseMap()
                .ForMember(d => d.Role, o => o.MapFrom(s => s.AppRole));

        CreateMap<ChangedUser, Data.User>();
        CreateMap<UserRegistration, Data.User>();
        CreateMap<Data.ApiKey, ApiKey>();
    }
}
