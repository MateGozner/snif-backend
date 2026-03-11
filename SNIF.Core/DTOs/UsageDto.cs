using SNIF.Core.Enums;
using SNIF.Core.Models;

namespace SNIF.Core.DTOs
{
    public record UsageResponseDto
    {
        public string UserId { get; init; } = null!;
        public DateTime Date { get; init; }
        public Dictionary<UsageType, int> UsageCounts { get; init; } = new();
        public PlanLimits CurrentLimits { get; init; } = null!;
        public SubscriptionPlan CurrentPlan { get; init; }
        public EntitlementSnapshotDto? Entitlement { get; init; }
    }
}
