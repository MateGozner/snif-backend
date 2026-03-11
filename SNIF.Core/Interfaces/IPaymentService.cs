using SNIF.Core.DTOs;

namespace SNIF.Core.Interfaces
{
    public interface IPaymentService
    {
        Task<string> CreateCheckoutSession(string userId, CreateCheckoutSessionDto dto);
        Task<string> CreateCreditPurchaseSession(string userId, PurchaseCreditsDto dto);
        Task<SubscriptionActivationStatusDto> RefreshSubscriptionActivationStatus(string userId);
        Task<string> CreatePortalSession(string userId);
        Task HandleWebhookEvent(string json, string signature);
    }
}
