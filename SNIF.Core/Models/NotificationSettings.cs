namespace SNIF.Core.Models
{
    public class NotificationSettings : BaseModel
    {
        public bool EmailNotifications { get; set; } = true;
        public bool PushNotifications { get; set; } = true;
        public bool NewMatchNotifications { get; set; } = true;
        public bool MessageNotifications { get; set; } = true;
        public bool BreedingRequestNotifications { get; set; } = true;
        public bool PlaydateRequestNotifications { get; set; } = true;
        
        // Daily notification time window
        public TimeSpan? NotificationStartTime { get; set; }
        public TimeSpan? NotificationEndTime { get; set; }
    }
}