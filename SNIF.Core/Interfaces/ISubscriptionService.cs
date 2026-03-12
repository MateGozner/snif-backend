using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;

namespace SNIF.Core.Interfaces
{
    public interface ISubscriptionService
    {
        Task<SubscriptionDto?> GetSubscription(string userId);
        Task<Subscription> CreateOrUpdateSubscription(string userId, SubscriptionPlan plan, string? providerSubscriptionId, string? providerCustomerId);
        Task CancelSubscription(string userId);
        Task HandleSubscriptionUpdated(string providerSubscriptionId, SubscriptionStatus status, DateTime periodStart, DateTime periodEnd, bool cancelAtPeriodEnd);
        Task HandleSubscriptionDeleted(string providerSubscriptionId);
    }
}
