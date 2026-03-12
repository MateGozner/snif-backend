using Microsoft.EntityFrameworkCore;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Infrastructure.Data;

namespace SNIF.Busniess.Services
{
    public class UsageService : IUsageService
    {
        private readonly SNIFContext _context;
        private readonly IEntitlementService _entitlementService;

        public UsageService(SNIFContext context, IEntitlementService entitlementService)
        {
            _context = context;
            _entitlementService = entitlementService;
        }

        public async Task RecordUsage(string userId, UsageType type)
        {
            var today = DateTime.UtcNow.Date;

            var record = await _context.UsageRecords
                .FirstOrDefaultAsync(u => u.UserId == userId && u.Type == type && u.Date == today);

            if (record != null)
            {
                record.Count++;
                record.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                record = new UsageRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Type = type,
                    Count = 1,
                    Date = today,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.UsageRecords.Add(record);
            }

            await _context.SaveChangesAsync();
        }

        public async Task<UsageResponseDto> GetDailyUsage(string userId, DateTime date)
        {
            var targetDate = date.Date;
            var entitlement = await _entitlementService.GetEntitlementAsync(userId);

            var records = await _context.UsageRecords
                .AsNoTracking()
                .Where(u => u.UserId == userId && u.Date == targetDate)
                .ToListAsync();

            var usageCounts = new Dictionary<UsageType, int>();
            foreach (var usageType in Enum.GetValues<UsageType>())
            {
                var record = records.FirstOrDefault(r => r.Type == usageType);
                usageCounts[usageType] = record?.Count ?? 0;
            }

            return new UsageResponseDto
            {
                UserId = userId,
                Date = targetDate,
                UsageCounts = usageCounts,
                CurrentLimits = entitlement.Limits,
                CurrentPlan = entitlement.EffectivePlan,
                Entitlement = entitlement
            };
        }

        public async Task<UsageCheckResult> CanPerformAction(string userId, UsageType type)
        {
            var entitlement = await _entitlementService.GetEntitlementAsync(userId);
            var limits = entitlement.Limits;

            // Hard feature gates — never credit-eligible
            if (type == UsageType.VideoCall && !limits.VideoCallEnabled)
                return new UsageCheckResult { Allowed = false, Source = UsageSource.Denied };

            if (type == UsageType.VideoCall && limits.VideoCallEnabled)
                return new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota };

            if (type == UsageType.Like && limits.UnlimitedLikes)
                return new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota };

            // Structural limits — never credit-eligible
            if (type == UsageType.PetCreation)
            {
                var allowed = entitlement.TotalPets < limits.MaxPets && !entitlement.IsOverPetLimit;
                return new UsageCheckResult { Allowed = allowed, Source = allowed ? UsageSource.PlanQuota : UsageSource.Denied };
            }

            // Daily-counted types (Like, SuperSniff) — credit-eligible
            var today = DateTime.UtcNow.Date;
            var record = await _context.UsageRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId && u.Type == type && u.Date == today);

            var currentCount = record?.Count ?? 0;
            var dailyLimit = type switch
            {
                UsageType.Like => limits.DailyLikes,
                UsageType.SuperSniff => limits.DailySuperSniffs,
                _ => 0
            };

            // Within plan quota — no credit needed
            if (currentCount < dailyLimit)
                return new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota };

            // Plan exhausted — attempt credit deduction (atomic)
            var (deducted, remaining) = await TryConsumeCredit(userId);
            if (deducted)
                return new UsageCheckResult { Allowed = true, Source = UsageSource.Credit, RemainingCredits = remaining };

            return new UsageCheckResult { Allowed = false, Source = UsageSource.Denied };
        }

        private async Task<(bool Deducted, int RemainingCredits)> TryConsumeCredit(string userId)
        {
            // Quick check: does the user have a credit balance row with credits?
            var balance = await _context.CreditBalances
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (balance == null || balance.Credits <= 0)
                return (false, 0);

            // Atomic UPDATE: deduct 1 credit only if balance > 0
            // Single row-level lock in PostgreSQL — no race condition possible
            var rowsAffected = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE ""CreditBalances"" SET ""Credits"" = ""Credits"" - 1, ""UpdatedAt"" = {0} WHERE ""UserId"" = {1} AND ""Credits"" > 0",
                DateTime.UtcNow, userId);

            if (rowsAffected == 0)
                return (false, 0);

            // Read back the remaining balance
            var remaining = await _context.CreditBalances
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .Select(c => c.Credits)
                .FirstOrDefaultAsync();

            return (true, remaining);
        }

        public PlanLimits GetLimitsForPlan(SubscriptionPlan plan)
        {
            return PlanLimits.GetLimits(plan);
        }
    }
}
