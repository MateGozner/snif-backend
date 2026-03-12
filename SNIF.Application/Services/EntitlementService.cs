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

            // Query active boosts
            var activeBoosts = await _context.UserBoosts
                .AsNoTracking()
                .Where(b => b.UserId == userId && b.ExpiresAt > now)
                .OrderByDescending(b => b.ActivatedAt)
                .ToListAsync();

            // Merge boost overrides into limits
            var mergedLimits = MergeBoostOverrides(limits, activeBoosts);

            var activeBoostDtos = activeBoosts.Select(b => new BoostDto
            {
                Id = b.Id,
                BoostType = b.BoostType,
                DurationDays = b.DurationDays,
                ActivatedAt = b.ActivatedAt,
                ExpiresAt = b.ExpiresAt,
                Source = b.Source,
                CreditsCost = b.CreditsCost
            }).ToArray();

            var petStates = pets
                .Select((pet, index) =>
                {
                    var isLocked = index >= mergedLimits.MaxPets;
                    return new PetEntitlementStateDto
                    {
                        PetId = pet.Id,
                        PetName = pet.Name,
                        CreatedAt = pet.CreatedAt,
                        IsLocked = isLocked,
                        LockReason = isLocked
                            ? $"Locked because this account exceeds the {effectivePlan} plan pet limit of {mergedLimits.MaxPets}. Oldest pets stay active after downgrade or expiry."
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
                Limits = mergedLimits,
                TotalPets = petStates.Length,
                ActivePets = Math.Min(petStates.Length, mergedLimits.MaxPets),
                LockedPets = lockedPetIds.Length,
                IsOverPetLimit = petStates.Length > mergedLimits.MaxPets,
                LockedPetIds = lockedPetIds,
                PetStates = petStates,
                ActiveBoosts = activeBoostDtos
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

        private static PlanLimits MergeBoostOverrides(PlanLimits baseLimits, List<Core.Entities.UserBoost> activeBoosts)
        {
            if (activeBoosts.Count == 0)
                return baseLimits;

            var merged = new PlanLimits
            {
                MaxPets = baseLimits.MaxPets,
                DailyLikes = baseLimits.DailyLikes,
                DailySuperSniffs = baseLimits.DailySuperSniffs,
                SearchRadiusKm = baseLimits.SearchRadiusKm,
                VideoCallEnabled = baseLimits.VideoCallEnabled,
                HasAds = baseLimits.HasAds,
                UnlimitedLikes = baseLimits.UnlimitedLikes
            };

            foreach (var boost in activeBoosts)
            {
                switch (boost.BoostType)
                {
                    case Core.Enums.BoostType.Radius50:
                        merged.SearchRadiusKm = Math.Max(merged.SearchRadiusKm, 50);
                        break;
                    case Core.Enums.BoostType.VideoChat:
                        merged.VideoCallEnabled = true;
                        break;
                }
            }

            return merged;
        }
    }
}