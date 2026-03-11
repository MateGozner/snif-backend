using Microsoft.EntityFrameworkCore;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Infrastructure.Data;

namespace SNIF.Application.Services
{
    public class EntitlementService : IEntitlementService
    {
        private readonly SNIFContext _context;

        public EntitlementService(SNIFContext context)
        {
            _context = context;
        }

        public async Task<EntitlementSnapshotDto> GetEntitlementAsync(string userId)
        {
            var subscription = await _context.Subscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .ThenByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            var pets = await _context.Pets
                .AsNoTracking()
                .Where(p => p.OwnerId == userId)
                .OrderBy(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .Select(p => new { p.Id, p.Name, p.CreatedAt })
                .ToListAsync();

            var now = DateTime.UtcNow;
            var billingPlan = subscription?.PlanId ?? SubscriptionPlan.Free;
            var effectiveStatus = ResolveEffectiveStatus(subscription, now);
            var effectivePlan = ResolveEffectivePlan(subscription, now, billingPlan, effectiveStatus);
            var limits = PlanLimits.GetLimits(effectivePlan);

            var petStates = pets
                .Select((pet, index) =>
                {
                    var isLocked = index >= limits.MaxPets;
                    return new PetEntitlementStateDto
                    {
                        PetId = pet.Id,
                        PetName = pet.Name,
                        CreatedAt = pet.CreatedAt,
                        IsLocked = isLocked,
                        LockReason = isLocked
                            ? $"Locked because this account exceeds the {effectivePlan} plan pet limit of {limits.MaxPets}. Oldest pets stay active after downgrade or expiry."
                            : null
                    };
                })
                .ToArray();

            var lockedPetIds = petStates
                .Where(p => p.IsLocked)
                .Select(p => p.PetId)
                .ToArray();

            return new EntitlementSnapshotDto
            {
                BillingPlan = billingPlan,
                EffectivePlan = effectivePlan,
                EffectiveStatus = effectiveStatus,
                SubscriptionStatus = subscription?.Status,
                CurrentPeriodStart = subscription?.CurrentPeriodStart,
                CurrentPeriodEnd = subscription?.CurrentPeriodEnd,
                CancelAtPeriodEnd = subscription?.CancelAtPeriodEnd ?? false,
                DowngradeEffectiveAt = ResolveDowngradeEffectiveAt(subscription, now, effectiveStatus),
                Limits = limits,
                TotalPets = petStates.Length,
                ActivePets = Math.Min(petStates.Length, limits.MaxPets),
                LockedPets = lockedPetIds.Length,
                IsOverPetLimit = petStates.Length > limits.MaxPets,
                LockedPetIds = lockedPetIds,
                PetStates = petStates
            };
        }

        public async Task EnsurePetCanUsePremiumActionsAsync(string userId, string petId, string actionName)
        {
            var entitlement = await GetEntitlementAsync(userId);
            var petState = entitlement.PetStates.FirstOrDefault(p => p.PetId == petId);

            if (petState == null)
            {
                throw new KeyNotFoundException("Pet not found for the current user.");
            }

            if (petState.IsLocked)
            {
                throw new InvalidOperationException(
                    $"Pet {petState.PetName} is locked for {actionName}. {petState.LockReason}");
            }
        }

        public bool IsPetLocked(EntitlementSnapshotDto entitlement, string petId)
        {
            return entitlement.PetStates.Any(p => p.PetId == petId && p.IsLocked);
        }

        private static EntitlementStatus ResolveEffectiveStatus(Subscription? subscription, DateTime now)
        {
            if (subscription == null)
            {
                return EntitlementStatus.Free;
            }

            var stillInCurrentPeriod = subscription.CurrentPeriodEnd > now;

            return subscription.Status switch
            {
                SubscriptionStatus.Active when subscription.CancelAtPeriodEnd && stillInCurrentPeriod => EntitlementStatus.Downgrading,
                SubscriptionStatus.Active when stillInCurrentPeriod => EntitlementStatus.Active,
                SubscriptionStatus.Trialing when stillInCurrentPeriod => EntitlementStatus.Trialing,
                SubscriptionStatus.PastDue when stillInCurrentPeriod => EntitlementStatus.PastDueGrace,
                _ => EntitlementStatus.Downgraded
            };
        }

        private static SubscriptionPlan ResolveEffectivePlan(
            Subscription? subscription,
            DateTime now,
            SubscriptionPlan billingPlan,
            EntitlementStatus effectiveStatus)
        {
            if (subscription == null)
            {
                return SubscriptionPlan.Free;
            }

            return effectiveStatus switch
            {
                EntitlementStatus.Active => billingPlan,
                EntitlementStatus.Trialing => billingPlan,
                EntitlementStatus.Downgrading => billingPlan,
                EntitlementStatus.PastDueGrace when subscription.CurrentPeriodEnd > now => billingPlan,
                _ => SubscriptionPlan.Free
            };
        }

        private static DateTime? ResolveDowngradeEffectiveAt(Subscription? subscription, DateTime now, EntitlementStatus effectiveStatus)
        {
            if (subscription == null)
            {
                return null;
            }

            if (effectiveStatus == EntitlementStatus.Downgrading || effectiveStatus == EntitlementStatus.PastDueGrace)
            {
                return subscription.CurrentPeriodEnd > now ? subscription.CurrentPeriodEnd : null;
            }

            return effectiveStatus == EntitlementStatus.Downgraded
                ? subscription.CurrentPeriodEnd
                : null;
        }
    }
}