using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Models;

public class MedicalHistoryMappingProfile : Profile
{
    public MedicalHistoryMappingProfile()
    {
        CreateMap<MedicalHistory, MedicalHistoryDto>();
        CreateMap<CreateMedicalHistoryDto, MedicalHistory>();
        CreateMap<UpdateMedicalHistoryDto, MedicalHistory>()
            .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
    }
}