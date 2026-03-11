using SNIF.Core.Entities;

namespace SNIF.Core.Entities
{
    public class Notification : BaseEntity
    {
        public string UserId { get; set; } = null!;
        public User User { get; set; } = null!;
        public string Type { get; set; } = null!;  // "match", "message", "system"
        public string Title { get; set; } = null!;
        public string Body { get; set; } = null!;
        public string? Data { get; set; }  // JSON string with extra data
        public bool IsRead { get; set; }
    }
}
