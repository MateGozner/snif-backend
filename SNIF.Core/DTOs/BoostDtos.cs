using System.ComponentModel.DataAnnotations;
using SNIF.Core.Enums;

namespace SNIF.Core.DTOs
{
    public record BoostDto
    {
        public string Id { get; init; } = null!;
        public BoostType BoostType { get; init; }
        public int DurationDays { get; init; }
        public DateTime ActivatedAt { get; init; }
        public DateTime ExpiresAt { get; init; }
        public BoostSource Source { get; init; }
        public int? CreditsCost { get; init; }
    }

    public record BoostPurchaseResultDto
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public BoostDto? Boost { get; init; }
        public int RemainingCredits { get; init; }
    }

    public record BoostOptionDto
    {
        public BoostType BoostType { get; init; }
        public string Description { get; init; } = null!;
        public int DurationDays { get; init; }
        public int? CreditCost { get; init; }
        public decimal? EuroPrice { get; init; }
        public bool AlreadyIncludedInPlan { get; init; }
    }

    public record AvailableBoostsDto
    {
        public IReadOnlyList<BoostOptionDto> Options { get; init; } = Array.Empty<BoostOptionDto>();
        public int CurrentCredits { get; init; }
        public IReadOnlyList<BoostDto> ActiveBoosts { get; init; } = Array.Empty<BoostDto>();
    }

    public record PurchaseBoostWithCreditsDto
    {
        [Required]
        public BoostType BoostType { get; init; }

        [Required]
        [Range(1, 7)]
        public int DurationDays { get; init; }
    }

    public record CreateDayPassCheckoutDto
    {
        [Required]
        public BoostType BoostType { get; init; }

        [Required]
        [Range(1, 7)]
        public int DurationDays { get; init; }

        public string? SuccessUrl { get; init; }
    }
}
