using SNIF.Core.Enums;

namespace SNIF.Core.Entities
{
    public class Subscription : BaseEntity
    {
        public string UserId { get; set; } = null!;
        public virtual User User { get; set; } = null!;

        public SubscriptionPlan PlanId { get; set; }
        public string? PaymentProviderSubscriptionId { get; set; }
        public string? PaymentProviderCustomerId { get; set; }

        public SubscriptionStatus Status { get; set; }
        public DateTime CurrentPeriodStart { get; set; }
        public DateTime CurrentPeriodEnd { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
    }
}
