using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{

    public record PreferencesDto
    {
        public double SearchRadius { get; init; }
        public bool ShowOnlineStatus { get; init; }
        public NotificationSettingsDto? NotificationSettings { get; init; }
    }

    public record NotificationSettingsDto
    {
        public bool EmailNotifications { get; init; }
        public bool PushNotifications { get; init; }
        public bool NewMatchNotifications { get; init; }
        public bool MessageNotifications { get; init; }
        public bool BreedingRequestNotifications { get; init; }
        public bool PlaydateRequestNotifications { get; init; }
        public TimeSpan? NotificationStartTime { get; init; }
        public TimeSpan? NotificationEndTime { get; init; }
    }
    public record UpdatePreferencesDto
    {
        [Range(1, 500)]
        public double SearchRadius { get; init; }
        public UpdateNotificationSettingsDto? NotificationSettings { get; init; }
        public bool ShowOnlineStatus { get; init; } = true;
    }

    public record UpdateNotificationSettingsDto
    {
        public bool EmailNotifications { get; init; }
        public bool PushNotifications { get; init; }
        public bool NewMatchNotifications { get; init; }
        public bool MessageNotifications { get; init; }
        public bool BreedingRequestNotifications { get; init; }
        public bool PlaydateRequestNotifications { get; init; }
        public TimeSpan? NotificationStartTime { get; init; }
        public TimeSpan? NotificationEndTime { get; init; }
    }
}