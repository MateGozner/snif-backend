using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using SNIF.Core.Models;

namespace SNIF.Core.Interfaces
{
    public interface IUsageService
    {
        Task RecordUsage(string userId, UsageType type);
        Task<UsageResponseDto> GetDailyUsage(string userId, DateTime date);
        Task<UsageCheckResult> CanPerformAction(string userId, UsageType type);
        PlanLimits GetLimitsForPlan(SubscriptionPlan plan);
    }
}
