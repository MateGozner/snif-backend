using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;

namespace SNIF.Core.Mappings
{
    public class PetMappingProfile : Profile
    {
        public PetMappingProfile()
        {
            CreateMap<Pet, PetDto>();
        }
    }
}