using SNIF.Core.Enums;

namespace SNIF.Core.DTOs
{
    public record AdminDashboardDto
    {
        public int TotalUsers { get; init; }
        public int TotalPets { get; init; }
        public int TotalMatches { get; init; }
        public int ActiveSubscriptions { get; init; }
        public decimal RevenueThisMonth { get; init; }
        public int NewUsersToday { get; init; }
        public int NewUsersThisWeek { get; init; }
        public double MatchRate { get; init; }
        public List<BreedStatDto> TopBreeds { get; init; } = new();
        public List<DailyStatDto> UserGrowth { get; init; } = new();
        public List<DailyStatDto> MatchesOverTime { get; init; } = new();
    }

    public record BreedStatDto
    {
        public string Breed { get; init; } = null!;
        public int Count { get; init; }
    }

    public record DailyStatDto
    {
        public DateTime Date { get; init; }
        public int Count { get; init; }
    }

    public record AdminUserDto
    {
        public string Id { get; init; } = null!;
        public string Name { get; init; } = null!;
        public string Email { get; init; } = null!;
        public string? Role { get; init; }
        public int PetCount { get; init; }
        public bool IsOnline { get; init; }
        public bool IsBanned { get; init; }
        public DateTime? SuspendedUntil { get; init; }
        public DateTime CreatedAt { get; init; }
        public string? SubscriptionPlan { get; init; }
        public AdminUserSupportFlagsDto SupportFlags { get; init; } = new();
    }

    public record AdminUserSupportFlagsDto
    {
        public bool PendingActivation { get; init; }
        public bool PaidButStillFree { get; init; }
        public bool PastDue { get; init; }
        public bool CancelAtPeriodEnd { get; init; }
        public bool DowngradeOrLockedPets { get; init; }
        public int IssueCount { get; init; }
    }

    public record AdminUserSupportSummaryDto
    {
        public int FlaggedUsers { get; init; }
        public int PendingActivation { get; init; }
        public int PaidButStillFree { get; init; }
        public int PastDue { get; init; }
        public int CancelAtPeriodEnd { get; init; }
        public int DowngradeOrLockedPets { get; init; }
    }

    public record AdminUserListResultDto
    {
        public List<AdminUserDto> Items { get; init; } = new();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages { get; init; }
        public AdminUserSupportSummaryDto SupportSummary { get; init; } = new();
    }

    public record AdminUserDetailDto : AdminUserDto
    {
        public List<PetSummaryDto> Pets { get; init; } = new();
        public int MatchesCount { get; init; }
        public int ReportsCount { get; init; }
        public AdminSubscriptionSupportDto? SubscriptionSupport { get; init; }
    }

    public record AdminSubscriptionSupportDto
    {
        public SubscriptionPlan BillingPlan { get; init; } = SubscriptionPlan.Free;
        public SubscriptionPlan EffectivePlan { get; init; } = SubscriptionPlan.Free;
        public SubscriptionStatus? SubscriptionStatus { get; init; }
        public EntitlementStatus EffectiveStatus { get; init; } = EntitlementStatus.Free;
        public SubscriptionActivationState? ActivationState { get; init; }
        public string? ActivationMessage { get; init; }
        public DateTime? CurrentPeriodStart { get; init; }
        public DateTime? CurrentPeriodEnd { get; init; }
        public bool CancelAtPeriodEnd { get; init; }
        public DateTime? DowngradeEffectiveAt { get; init; }
        public bool IsOverPetLimit { get; init; }
        public int LockedPetCount { get; init; }
        public List<PetEntitlementStateDto> LockedPets { get; init; } = new();
        public string? PaymentProviderSubscriptionId { get; init; }
        public string? PaymentProviderCustomerId { get; init; }
        public bool PaidButStillFree { get; init; }
    }

    public record PetSummaryDto
    {
        public string Id { get; init; } = null!;
        public string Name { get; init; } = null!;
        public string Species { get; init; } = null!;
        public string Breed { get; init; } = null!;
    }

    public record AdminUserFilterDto
    {
        public string? Search { get; init; }
        public string? Role { get; init; }
        public string? Status { get; init; } // Active, Banned, Suspended
        public string? SupportIssue { get; init; } // any, pendingActivation, paidButStillFree, pastDue, cancelAtPeriodEnd, downgradeLockedPets
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 20;
        public string? SortBy { get; init; }
        public string? SortDirection { get; init; } // asc, desc
    }

    public record AdminReportDto
    {
        public string Id { get; init; } = null!;
        public string ReporterName { get; init; } = null!;
        public string TargetUserId { get; init; } = null!;
        public string TargetUserName { get; init; } = null!;
        public string? TargetPetName { get; init; }
        public ReportReason Reason { get; init; }
        public string? Description { get; init; }
        public ReportStatus Status { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public record ReportFilterDto
    {
        public ReportStatus? Status { get; init; }
        public ReportReason? Reason { get; init; }
        public int Page { get; init; } = 1;
        public int PageSize { get; init; } = 20;
    }

    public record AdminSubscriptionStatsDto
    {
        public int TotalFree { get; init; }
        public int TotalGoodBoy { get; init; }
        public int TotalAlphaPack { get; init; }
        public int TotalTreatBag { get; init; }
        public decimal Mrr { get; init; }
    }

    public record SystemHealthDto
    {
        public bool DbConnected { get; init; }
        public bool RedisConnected { get; init; }
        public int ActiveSignalRConnections { get; init; }
        public TimeSpan Uptime { get; init; }
        public string? LastMigration { get; init; }
    }

    public record PagedResult<T>
    {
        public List<T> Items { get; init; } = new();
        public int TotalCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public int TotalPages { get; init; }
    }

    public record SuspendUserDto
    {
        public int DurationDays { get; init; }
        public string Reason { get; init; } = null!;
    }

    public record BanUserDto
    {
        public string Reason { get; init; } = null!;
    }

    public record ResolveReportDto
    {
        public string Resolution { get; init; } = null!;
        public string? Notes { get; init; }
    }

    public record RegisterDeviceDto
    {
        public string Token { get; init; } = null!;
        public string Platform { get; init; } = null!;
    }

    public record UnregisterDeviceDto
    {
        public string Token { get; init; } = null!;
    }

    public record WarnUserDto
    {
        public string? Reason { get; init; }
    }

    public record DismissReportDto
    {
        public string? Notes { get; init; }
    }

    public record AdminSubscriptionDto
    {
        public string UserId { get; init; } = null!;
        public string UserName { get; init; } = null!;
        public string Plan { get; init; } = null!;
        public string Status { get; init; } = null!;
        public DateTime StartDate { get; init; }
        public DateTime EndDate { get; init; }
        public decimal Amount { get; init; }
    }

    public record RevenueDataPointDto
    {
        public string Month { get; init; } = null!;
        public decimal Revenue { get; init; }
        public int Subscriptions { get; init; }
    }
}
