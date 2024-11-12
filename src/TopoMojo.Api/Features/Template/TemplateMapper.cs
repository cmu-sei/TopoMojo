// Copyright 2025 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using AutoMapper;
using TopoMojo.Api.Models;

namespace TopoMojo.Api
{
    public class TemplateProfile : Profile
    {
        public TemplateProfile()
        {

            CreateMap<Data.Template, TemplateDetail>()
                .ReverseMap()
            ;

            CreateMap<Data.Template, TemplateSummary>();

            CreateMap<Data.Template, Template>()
                .ReverseMap()
            ;

            CreateMap<NewTemplateDetail, Data.Template>();

            CreateMap<ChangedTemplateDetail, Data.Template>();

            CreateMap<ChangedTemplate, Data.Template>();

            CreateMap<Data.Template, ConvergedTemplate>()
                .ForMember(d => d.Detail, opt => opt.MapFrom(s => s.Detail ?? s.Parent.Detail))
            ;

        }
    }
}
