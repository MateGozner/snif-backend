using SNIF.Core.Enums;

namespace SNIF.Core.Entities
{
    public class UsageRecord : BaseEntity
    {
        public string UserId { get; set; } = null!;
        public virtual User User { get; set; } = null!;

        public UsageType Type { get; set; }
        public int Count { get; set; }
        public DateTime Date { get; set; }
    }
}
