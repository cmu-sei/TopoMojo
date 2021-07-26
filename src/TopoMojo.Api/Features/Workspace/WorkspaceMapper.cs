// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.Text.Json;
using AutoMapper;
using TopoMojo.Api.Models;

namespace TopoMojo.Api
{
    public class WorkspaceProfile : Profile
    {
        public WorkspaceProfile()
        {
            CreateMap<Data.Workspace, Workspace>()
            ;

            CreateMap<Data.Workspace, WorkspaceSummary>()
            ;

            CreateMap<Data.Workspace, JoinCode>()
                .ForMember(d => d.Code, opt => opt.MapFrom(s => s.ShareCode))
            ;

            CreateMap<NewWorkspace, Data.Workspace>()
                .ForMember(d => d.Challenge, opt => opt.MapFrom(s => s.Challenge ??
                    JsonSerializer.Serialize<ChallengeSpec>(
                        new ChallengeSpec(),
                        null
                    )
                ))
            ;

            CreateMap<ChangedWorkspace, RestrictedChangedWorkspace>();

            CreateMap<RestrictedChangedWorkspace, Data.Workspace>();

            CreateMap<ChangedWorkspace, Data.Workspace>();

            CreateMap<Data.Worker, Worker>();

        }
    }
}
