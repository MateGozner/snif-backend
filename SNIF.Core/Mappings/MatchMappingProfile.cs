using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Mappings
{
    public class MatchMappingProfile : Profile
    {
        public MatchMappingProfile()
        {
            CreateMap<Match, MatchDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.InitiatorPet, opt => opt.MapFrom(src => src.InitiatiorPet))
                .ForMember(dest => dest.TargetPet, opt => opt.MapFrom(src => src.TargetPet))
                .ForMember(dest => dest.MatchPurpose, opt => opt.MapFrom(src => src.Purpose))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.ExpiresAt, opt => opt.MapFrom(src => src.ExpiresAt));

            CreateMap<CreateMatchDto, Match>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.InitiatiorPetId, opt => opt.Ignore())
                .ForMember(dest => dest.TargetPetId, opt => opt.MapFrom(src => src.TargetPetId))
                .ForMember(dest => dest.Purpose, opt => opt.MapFrom(src => src.MatchPurpose))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => MatchStatus.Pending))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.ExpiresAt, opt => opt.MapFrom(src => DateTime.UtcNow.AddDays(7)));
        }
    }
}
