using AutoMapper;
using SNIF.Core.Contracts;
using SNIF.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Mappings
{
    public class NotificationMappingProfile : Profile
    {
        public NotificationMappingProfile()
        {
            CreateMap<Pet, PetMatchNotification>()
                .ForMember(dest => dest.MatchedPetId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.OwnerId, opt => opt.MapFrom(src => src.OwnerId))
                .ForMember(dest => dest.PetName, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.NotifiedAt, opt => opt.MapFrom(src => DateTime.UtcNow));
        }
    }
}
