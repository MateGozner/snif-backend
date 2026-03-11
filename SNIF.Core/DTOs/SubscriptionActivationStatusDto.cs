using SNIF.Core.Enums;

namespace SNIF.Core.DTOs
{
    public record SubscriptionActivationStatusDto
    {
        public SubscriptionActivationState State { get; init; }
        public string Message { get; init; } = string.Empty;
        public SubscriptionDto? Subscription { get; init; }
    }
}