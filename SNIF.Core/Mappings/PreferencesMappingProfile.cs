using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Models;

namespace SNIF.Core.Mappings
{
    public class PreferencesMappingProfile : Profile
    {
        public PreferencesMappingProfile()
        {
            CreateMap<UserPreferences, PreferencesDto>()
                .ForMember(dest => dest.NotificationSettings,
                    opt => opt.MapFrom(src => src.NotificationSettings));

            CreateMap<UpdatePreferencesDto, UserPreferences>()
                .ForMember(dest => dest.UserId, opt => opt.Ignore())
                .ForMember(dest => dest.User, opt => opt.Ignore())
                .ForMember(dest => dest.NotificationSettings,
                    opt => opt.MapFrom(src => src.NotificationSettings));

            CreateMap<NotificationSettings, NotificationSettingsDto>();
            CreateMap<UpdateNotificationSettingsDto, NotificationSettings>();
        }
    }
}