namespace SNIF.Core.Entities
{
    public class DeviceToken : BaseEntity
    {
        public string UserId { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public string Token { get; set; } = null!;
        public string Platform { get; set; } = null!; // "android", "ios", "web"
        public DateTime LastUsedAt { get; set; }
    }
}
