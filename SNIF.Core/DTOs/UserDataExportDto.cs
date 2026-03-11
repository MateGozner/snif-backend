namespace SNIF.Core.DTOs
{
    public record UserDataExportDto
    {
        public DateTime ExportDate { get; init; }
        public string UserId { get; init; } = null!;
        public UserProfileExportDto Profile { get; init; } = null!;
        public ICollection<PetDto> Pets { get; init; } = new List<PetDto>();
        public ICollection<MatchDto> Matches { get; init; } = new List<MatchDto>();
        public ICollection<MessageDto> Messages { get; init; } = new List<MessageDto>();
        public ICollection<SubscriptionDto> Subscriptions { get; init; } = new List<SubscriptionDto>();
        public ICollection<UsageExportDto> Usage { get; init; } = new List<UsageExportDto>();
        public ICollection<ReportExportDto> Reports { get; init; } = new List<ReportExportDto>();
    }

    public record UserProfileExportDto
    {
        public string Name { get; init; } = null!;
        public string Email { get; init; } = null!;
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public bool IsOnline { get; init; }
        public DateTime? LastSeen { get; init; }
        public string? ProfilePicturePath { get; init; }
        public LocationDto? Location { get; init; }
        public PreferencesDto? Preferences { get; init; }
    }

    public record UsageExportDto
    {
        public string Type { get; init; } = null!;
        public int Count { get; init; }
        public DateTime Date { get; init; }
    }

    public record ReportExportDto
    {
        public string Id { get; init; } = null!;
        public string TargetUserId { get; init; } = null!;
        public string Reason { get; init; } = null!;
        public string? Description { get; init; }
        public string Status { get; init; } = null!;
        public DateTime CreatedAt { get; init; }
    }
}
