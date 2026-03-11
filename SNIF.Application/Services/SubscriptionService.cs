using Microsoft.EntityFrameworkCore;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Infrastructure.Data;

namespace SNIF.Busniess.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly SNIFContext _context;

        public SubscriptionService(SNIFContext context)
        {
            _context = context;
        }

        public async Task<SubscriptionDto?> GetSubscription(string userId)
        {
            var sub = await _context.Subscriptions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status != SubscriptionStatus.Canceled);

            if (sub == null)
                return null;

            return MapToDto(sub);
        }

        public async Task<Subscription> CreateOrUpdateSubscription(
            string userId,
            SubscriptionPlan plan,
            string? providerSubscriptionId,
            string? providerCustomerId)
        {
            var existing = await _context.Subscriptions
                .FirstOrDefaultAsync(s =>
                    (!string.IsNullOrEmpty(providerSubscriptionId)
                        && s.PaymentProviderSubscriptionId == providerSubscriptionId)
                    || (s.UserId == userId && s.Status != SubscriptionStatus.Canceled));

            if (existing == null)
            {
                existing = await _context.Subscriptions
                    .Where(s => s.UserId == userId)
                    .OrderByDescending(s => s.UpdatedAt)
                    .FirstOrDefaultAsync();
            }

            if (existing != null)
            {
                existing.PlanId = plan;
                existing.PaymentProviderSubscriptionId = providerSubscriptionId;
                existing.PaymentProviderCustomerId = providerCustomerId;
                existing.Status = SubscriptionStatus.Active;
                existing.CurrentPeriodStart = DateTime.UtcNow;
                existing.CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1);
                existing.CancelAtPeriodEnd = false;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                existing = new Subscription
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    PlanId = plan,
                    PaymentProviderSubscriptionId = providerSubscriptionId,
                    PaymentProviderCustomerId = providerCustomerId,
                    Status = SubscriptionStatus.Active,
                    CurrentPeriodStart = DateTime.UtcNow,
                    CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
                    CancelAtPeriodEnd = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Subscriptions.Add(existing);
            }

            await _context.SaveChangesAsync();
            return existing;
        }

        public async Task CancelSubscription(string userId)
        {
            var sub = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active);

            if (sub == null)
                throw new InvalidOperationException("No active subscription found.");

            sub.CancelAtPeriodEnd = true;
            sub.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task HandleSubscriptionUpdated(
            string providerSubscriptionId,
            SubscriptionStatus status,
            DateTime periodStart,
            DateTime periodEnd,
            bool cancelAtPeriodEnd)
        {
            var sub = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.PaymentProviderSubscriptionId == providerSubscriptionId);

            if (sub == null)
                return;

            sub.Status = status;
            sub.CurrentPeriodStart = periodStart;
            sub.CurrentPeriodEnd = periodEnd;
            sub.CancelAtPeriodEnd = cancelAtPeriodEnd;
            sub.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task HandleSubscriptionDeleted(string providerSubscriptionId)
        {
            var sub = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.PaymentProviderSubscriptionId == providerSubscriptionId);

            if (sub == null)
                return;

            sub.Status = SubscriptionStatus.Canceled;
            sub.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        private static SubscriptionDto MapToDto(Subscription sub) => new()
        {
            Id = sub.Id,
            UserId = sub.UserId,
            PlanId = sub.PlanId,
            Status = sub.Status,
            CurrentPeriodStart = sub.CurrentPeriodStart,
            CurrentPeriodEnd = sub.CurrentPeriodEnd,
            CancelAtPeriodEnd = sub.CancelAtPeriodEnd,
            PaymentProviderCustomerId = sub.PaymentProviderCustomerId
        };
    }
}
