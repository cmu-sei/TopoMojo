// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using TopoMojo.Api.Models;
using TopoMojo.Api.Data.Extensions;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api
{
    public class GamespaceProfile : Profile
    {
        public GamespaceProfile()
        {
            CreateMap<Data.Gamespace, Gamespace>()
            ;

            CreateMap<ChangedGamespace, Data.Gamespace>()
            ;

            CreateMap<Data.Gamespace, GameState>()
                .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Workspace.Name))
                .ForMember(d => d.Challenge, opt => opt.Ignore())
            ;

            CreateMap<Data.Gamespace, GameStateSummary>()
            ;

            CreateMap<GameStateSummary, GameState>()
            ;

            CreateMap<Data.Player, Player>()
            ;

            CreateMap<ChallengeSpec, ChallengeView>()
            ;

            CreateMap<SectionSpec, SectionView>()
            ;

            CreateMap<QuestionSpec, QuestionView>()
                .ForMember(d => d.Answer, opt => opt.Ignore())
            ;
        }
    }
}
