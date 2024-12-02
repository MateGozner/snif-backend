using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;

namespace SNIF.Core.Mappings
{
    public class UserMappingProfile : Profile
    {
        public UserMappingProfile()
        {
            CreateMap<User, UserDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty))
                .ForMember(dest => dest.Location, opt => opt.MapFrom(src => src.Location))
                .ForMember(dest => dest.Pets, opt => opt.MapFrom(src => src.Pets ?? new List<Pet>()))
                .ForMember(dest => dest.Preferences, opt => opt.MapFrom(src => src.Preferences))
                .ForMember(dest => dest.ProfilePicturePath, opt => opt.MapFrom(src =>
                    src.ProfilePicturePath != null ?
                    $"/api/user/profile-picture/{Path.GetFileName(src.ProfilePicturePath)}" : null))
                .PreserveReferences();  // Handle circular references

            CreateMap<User, AuthResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name ?? string.Empty))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.Token, opt => opt.Ignore());
        }
    }
}