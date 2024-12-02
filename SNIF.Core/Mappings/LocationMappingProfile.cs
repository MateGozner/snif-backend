using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Models;

namespace SNIF.Core.Mappings
{
    public class LocationMappingProfile : Profile
    {
        public LocationMappingProfile()
        {
            CreateMap<Location, LocationDto>();
            CreateMap<LocationDto, Location>()
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow));
        }
    }
}
