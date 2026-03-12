namespace SNIF.Core.Entities
{
    public class PaymentTransaction : BaseEntity
    {
        public string? UserId { get; set; }
        public string EventName { get; set; } = null!;
        public string? ProviderOrderId { get; set; }
        public string? ProviderSubscriptionId { get; set; }
        public string? ProviderCustomerId { get; set; }
        public decimal? AmountCents { get; set; }
        public string? Currency { get; set; }
        public string? Status { get; set; }
        public string? PlanName { get; set; }
        public string? ProductType { get; set; }
    }
}
