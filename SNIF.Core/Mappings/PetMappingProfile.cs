using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;

namespace SNIF.Core.Mappings
{
    public class PetMappingProfile : Profile
    {
        private const string BaseUrl = "http://localhost:5000";
        public PetMappingProfile()
        {
            CreateMap<Pet, PetDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Media, opt => opt.MapFrom(src => src.Media))
                .ForMember(dest => dest.Links, opt => opt.MapFrom((src, dest, _, context) =>
                    new Dictionary<string, string>
                    {
                        ["self"] = $"/api/pets/{src.Id}",
                        ["media"] = $"/api/pets/{src.Id}/media",
                        ["owner"] = $"/api/users/{src.OwnerId}"
                    }));

            CreateMap<CreatePetDto, Pet>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => Guid.NewGuid().ToString()))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Owner, opt => opt.Ignore())
                .ForMember(dest => dest.Media, opt => opt.Ignore());

            CreateMap<PetMedia, MediaResponseDto>()
                .ForMember(dest => dest.Url, opt => opt.MapFrom((src, _, _, context) =>
                $"{BaseUrl}/uploads/pets/{(src.Type == MediaType.Photo ? "photos" : "videos")}/{src.FileName}"))
                .ForMember(dest => dest.Links, opt => opt.MapFrom((src, _, _, context) =>
                    new Dictionary<string, string>
                    {
                        ["self"] = $"/api/pets/{src.PetId}/media/{src.Id}",
                        ["pet"] = $"/api/pets/{src.PetId}"
                    }));

            CreateMap<UpdatePetDto, Pet>()
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}