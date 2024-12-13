using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;

namespace SNIF.Core.Mappings
{
    public class PetMappingProfile : Profile
    {
        public PetMappingProfile()
        {
            CreateMap<Pet, PetDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Photos, opt => opt.MapFrom(src =>
                    src.Photos.Select(p => $"/api/Pet/photos/{p}").ToList()))
                .ForMember(dest => dest.Videos, opt => opt.MapFrom(src =>
                    src.Videos.Select(v => $"/api/Pet/videos/{v}").ToList()));

            CreateMap<CreatePetDto, Pet>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid().ToString()))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Owner, opt => opt.Ignore())
                .ForMember(dest => dest.Photos, opt => opt.Ignore())
                .ForMember(dest => dest.Videos, opt => opt.Ignore());

            CreateMap<UpdatePetDto, Pet>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}