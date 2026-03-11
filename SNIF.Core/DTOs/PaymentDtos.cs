using System.ComponentModel.DataAnnotations;
using SNIF.Core.Enums;

namespace SNIF.Core.DTOs
{
    public record CreateCheckoutSessionDto
    {
        [Required]
        public SubscriptionPlan Plan { get; init; }

        [Required]
        public BillingInterval BillingInterval { get; init; } = BillingInterval.Monthly;

        /// <summary>Optional success redirect URL.</summary>
        public string? SuccessUrl { get; init; }

        /// <summary>Optional cancel redirect URL.</summary>
        public string? CancelUrl { get; init; }
    }

    public record PurchaseCreditsDto
    {
        /// <summary>Credit pack amount: 10, 50, or 100.</summary>
        [Required]
        public int Amount { get; init; }

        /// <summary>Optional success redirect URL.</summary>
        public string? SuccessUrl { get; init; }

        /// <summary>Optional cancel redirect URL.</summary>
        public string? CancelUrl { get; init; }
    }

    public record CreditBalanceDto
    {
        public int Credits { get; init; }
        public DateTime? LastPurchasedAt { get; init; }
    }
}
