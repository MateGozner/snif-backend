using Microsoft.EntityFrameworkCore;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Infrastructure.Data;

namespace SNIF.Busniess.Services
{
    public class BoostService : IBoostService
    {
        private readonly SNIFContext _context;

        private static readonly Dictionary<(BoostType, int), int> CreditCosts = new()
        {
            [(BoostType.Radius50, 1)] = 5,
            [(BoostType.Radius50, 3)] = 12,
            [(BoostType.Radius50, 7)] = 25,
            [(BoostType.VideoChat, 1)] = 8,
            [(BoostType.VideoChat, 3)] = 20,
            [(BoostType.VideoChat, 7)] = 40,
        };

        private static readonly Dictionary<(BoostType, int), decimal> EuroPrices = new()
        {
            [(BoostType.Radius50, 1)] = 0.99m,
            [(BoostType.Radius50, 3)] = 1.99m,
            [(BoostType.Radius50, 7)] = 3.99m,
            [(BoostType.VideoChat, 1)] = 1.49m,
            [(BoostType.VideoChat, 3)] = 2.99m,
            [(BoostType.VideoChat, 7)] = 5.99m,
        };

        public BoostService(SNIFContext context)
        {
            _context = context;
        }

        public async Task<BoostPurchaseResultDto> PurchaseWithCredits(string userId, BoostType boostType, int durationDays)
        {
            if (!CreditCosts.TryGetValue((boostType, durationDays), out var cost))
            {
                return new BoostPurchaseResultDto
                {
                    Success = false,
                    Message = $"Invalid boost configuration: {boostType}, {durationDays} days."
                };
            }

            // Atomic deduction: only succeeds if user has enough credits
            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                "UPDATE \"CreditBalances\" SET \"Credits\" = \"Credits\" - {0}, \"UpdatedAt\" = {1} WHERE \"UserId\" = {2} AND \"Credits\" >= {3}",
                cost, DateTime.UtcNow, userId, cost);

            if (rowsAffected == 0)
            {
                var currentBalance = await _context.CreditBalances
                    .AsNoTracking()
                    .Where(c => c.UserId == userId)
                    .Select(c => c.Credits)
                    .FirstOrDefaultAsync();

                return new BoostPurchaseResultDto
                {
                    Success = false,
                    Message = $"Insufficient credits. Required: {cost}, available: {currentBalance}.",
                    RemainingCredits = currentBalance
                };
            }

            var now = DateTime.UtcNow;
            var boost = new UserBoost
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                BoostType = boostType,
                DurationDays = durationDays,
                ActivatedAt = now,
                ExpiresAt = now.AddDays(durationDays),
                Source = BoostSource.Credit,
                CreditsCost = cost,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.UserBoosts.Add(boost);
            await _context.SaveChangesAsync();

            var remainingCredits = await _context.CreditBalances
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => c.Credits)
                .FirstOrDefaultAsync();

            return new BoostPurchaseResultDto
            {
                Success = true,
                Message = "Boost activated successfully.",
                Boost = MapToDto(boost),
                RemainingCredits = remainingCredits
            };
        }

        public async Task<BoostDto> ActivateFromOrder(string userId, BoostType boostType, int durationDays, string orderId)
        {
            // Idempotency: check if this order was already processed
            var existing = await _context.UserBoosts
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.LemonSqueezyOrderId == orderId);

            if (existing != null)
            {
                return MapToDto(existing);
            }

            var now = DateTime.UtcNow;
            var boost = new UserBoost
            {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                BoostType = boostType,
                DurationDays = durationDays,
                ActivatedAt = now,
                ExpiresAt = now.AddDays(durationDays),
                Source = BoostSource.Purchase,
                LemonSqueezyOrderId = orderId,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.UserBoosts.Add(boost);
            await _context.SaveChangesAsync();

            return MapToDto(boost);
        }

        public async Task<IReadOnlyList<BoostDto>> GetActiveBoosts(string userId)
        {
            var now = DateTime.UtcNow;
            var boosts = await _context.UserBoosts
                .AsNoTracking()
                .Where(b => b.UserId == userId && b.ExpiresAt > now)
                .OrderByDescending(b => b.ActivatedAt)
                .ToListAsync();

            return boosts.Select(MapToDto).ToList();
        }

        public async Task<AvailableBoostsDto> GetAvailableBoosts(string userId)
        {
            var subscription = await _context.Subscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .FirstOrDefaultAsync();

            var effectivePlan = subscription?.PlanId ?? SubscriptionPlan.Free;
            var limits = PlanLimits.GetLimits(effectivePlan);

            var currentCredits = await _context.CreditBalances
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => c.Credits)
                .FirstOrDefaultAsync();

            var activeBoosts = await GetActiveBoosts(userId);

            var options = new List<BoostOptionDto>();

            foreach (var entry in CreditCosts)
            {
                var (boostType, days) = entry.Key;
                var creditCost = entry.Value;
                EuroPrices.TryGetValue((boostType, days), out var euroPrice);

                var includedInPlan = !IsBoostRelevant(boostType, limits);

                options.Add(new BoostOptionDto
                {
                    BoostType = boostType,
                    Description = GetBoostDescription(boostType),
                    DurationDays = days,
                    CreditCost = creditCost,
                    EuroPrice = euroPrice,
                    AlreadyIncludedInPlan = includedInPlan
                });
            }

            return new AvailableBoostsDto
            {
                Options = options.OrderBy(o => o.BoostType).ThenBy(o => o.DurationDays).ToList(),
                CurrentCredits = currentCredits,
                ActiveBoosts = activeBoosts
            };
        }

        public async Task<bool> HasActiveBoost(string userId, BoostType boostType)
        {
            var now = DateTime.UtcNow;
            return await _context.UserBoosts
                .AsNoTracking()
                .AnyAsync(b => b.UserId == userId && b.BoostType == boostType && b.ExpiresAt > now);
        }

        private static bool IsBoostRelevant(BoostType boostType, PlanLimits limits) => boostType switch
        {
            BoostType.Radius50 => limits.SearchRadiusKm < 50,
            BoostType.VideoChat => !limits.VideoCallEnabled,
            _ => true
        };

        private static string GetBoostDescription(BoostType boostType) => boostType switch
        {
            BoostType.Radius50 => "Expand search radius to 50 km",
            BoostType.VideoChat => "Unlock video chat",
            _ => boostType.ToString()
        };

        private static BoostDto MapToDto(UserBoost boost) => new()
        {
            Id = boost.Id,
            BoostType = boost.BoostType,
            DurationDays = boost.DurationDays,
            ActivatedAt = boost.ActivatedAt,
            ExpiresAt = boost.ExpiresAt,
            Source = boost.Source,
            CreditsCost = boost.CreditsCost
        };
    }
}
