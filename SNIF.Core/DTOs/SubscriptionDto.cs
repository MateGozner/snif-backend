using SNIF.Core.Enums;
using SNIF.Core.Models;

namespace SNIF.Core.DTOs
{
    public record SubscriptionDto
    {
        public string Id { get; init; } = null!;
        public string UserId { get; init; } = null!;
        public SubscriptionPlan PlanId { get; init; }
        public SubscriptionStatus Status { get; init; }
        public DateTime CurrentPeriodStart { get; init; }
        public DateTime CurrentPeriodEnd { get; init; }
        public bool CancelAtPeriodEnd { get; init; }
        public string? PaymentProviderCustomerId { get; init; }
        public SubscriptionPlan EffectivePlanId { get; init; }
        public EntitlementStatus EffectiveStatus { get; init; }
        public DateTime? DowngradeEffectiveAt { get; init; }
        public PlanLimits EffectiveLimits { get; init; } = null!;
        public bool IsOverPetLimit { get; init; }
        public int LockedPetCount { get; init; }
        public IReadOnlyCollection<PetEntitlementStateDto> LockedPets { get; init; } = Array.Empty<PetEntitlementStateDto>();
    }
}
