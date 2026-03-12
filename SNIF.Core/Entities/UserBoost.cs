using SNIF.Core.Enums;

namespace SNIF.Core.Entities
{
    public class UserBoost : BaseEntity
    {
        public string UserId { get; set; } = null!;
        public virtual User User { get; set; } = null!;
        public BoostType BoostType { get; set; }
        public int DurationDays { get; set; }
        public DateTime ActivatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public BoostSource Source { get; set; }
        public string? LemonSqueezyOrderId { get; set; }
        public int? CreditsCost { get; set; }
    }
}
