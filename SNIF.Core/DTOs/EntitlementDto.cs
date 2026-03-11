using SNIF.Core.Enums;
using SNIF.Core.Models;

namespace SNIF.Core.DTOs
{
    public record PetEntitlementStateDto
    {
        public string PetId { get; init; } = null!;
        public string PetName { get; init; } = null!;
        public DateTime CreatedAt { get; init; }
        public bool IsLocked { get; init; }
        public string? LockReason { get; init; }
    }

    public record EntitlementSnapshotDto
    {
        public SubscriptionPlan BillingPlan { get; init; }
        public SubscriptionPlan EffectivePlan { get; init; }
        public EntitlementStatus EffectiveStatus { get; init; }
        public SubscriptionStatus? SubscriptionStatus { get; init; }
        public DateTime? CurrentPeriodStart { get; init; }
        public DateTime? CurrentPeriodEnd { get; init; }
        public bool CancelAtPeriodEnd { get; init; }
        public DateTime? DowngradeEffectiveAt { get; init; }
        public PlanLimits Limits { get; init; } = null!;
        public int TotalPets { get; init; }
        public int ActivePets { get; init; }
        public int LockedPets { get; init; }
        public bool IsOverPetLimit { get; init; }
        public IReadOnlyCollection<string> LockedPetIds { get; init; } = Array.Empty<string>();
        public IReadOnlyCollection<PetEntitlementStateDto> PetStates { get; init; } = Array.Empty<PetEntitlementStateDto>();
    }
}