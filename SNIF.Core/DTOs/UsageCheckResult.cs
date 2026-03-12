namespace SNIF.Core.DTOs
{
    public record UsageCheckResult
    {
        public bool Allowed { get; init; }
        public UsageSource Source { get; init; }
        public int? RemainingCredits { get; init; }
    }

    public enum UsageSource
    {
        PlanQuota,
        Credit,
        Denied
    }
}
