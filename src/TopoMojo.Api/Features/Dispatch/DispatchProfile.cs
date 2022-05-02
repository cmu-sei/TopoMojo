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
