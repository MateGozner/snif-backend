using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using System.Security.Claims;

namespace SNIF.API.Filters
{
    public class UsageEnforcementFilter : IAsyncActionFilter
    {
        private readonly UsageType _usageType;

        public UsageEnforcementFilter(UsageType usageType)
        {
            _usageType = usageType;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userId = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var usageService = context.HttpContext.RequestServices.GetRequiredService<IUsageService>();
            var entitlementService = context.HttpContext.RequestServices.GetRequiredService<IEntitlementService>();

            var result = await usageService.CanPerformAction(userId, _usageType);
            if (!result.Allowed)
            {
                var entitlement = await entitlementService.GetEntitlementAsync(userId);
                var errorBody = await BuildErrorResponse(userId, entitlement, usageService, result);

                context.Result = new ObjectResult(errorBody)
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            var executedContext = await next();

            // Record usage only if action completed successfully (no exception, not canceled)
            if (executedContext.Exception == null && !executedContext.Canceled)
            {
                await usageService.RecordUsage(userId, _usageType);

                // Enrich response with credit consumption metadata
                if (result.Source == UsageSource.Credit)
                {
                    context.HttpContext.Response.Headers["X-Credit-Consumed"] = "true";
                    context.HttpContext.Response.Headers["X-Credits-Remaining"] =
                        result.RemainingCredits?.ToString() ?? "0";
                }
            }
        }

        private async Task<object> BuildErrorResponse(
            string userId,
            EntitlementSnapshotDto entitlement,
            IUsageService usageService,
            UsageCheckResult checkResult)
        {
            var planName = entitlement.EffectivePlan.ToString();
            var usage = await usageService.GetDailyUsage(userId, DateTime.UtcNow);
            var creditBalance = checkResult.RemainingCredits ?? 0;

            return _usageType switch
            {
                UsageType.Like => new
                {
                    error = "DailyLikeLimit",
                    limit = entitlement.Limits.DailyLikes,
                    used = usage.UsageCounts.GetValueOrDefault(UsageType.Like),
                    plan = planName,
                    creditsAvailable = creditBalance,
                    upgradeUrl = "/pricing",
                    buyCreditsUrl = "/credits"
                },
                UsageType.PetCreation => new
                {
                    error = "PetLimit",
                    limit = entitlement.Limits.MaxPets,
                    current = entitlement.TotalPets,
                    lockedPets = entitlement.PetStates.Where(p => p.IsLocked),
                    plan = planName,
                    upgradeUrl = "/pricing"
                },
                UsageType.SuperSniff => new
                {
                    error = "DailySuperSniffLimit",
                    limit = entitlement.Limits.DailySuperSniffs,
                    used = usage.UsageCounts.GetValueOrDefault(UsageType.SuperSniff),
                    plan = planName,
                    creditsAvailable = creditBalance,
                    upgradeUrl = "/pricing",
                    buyCreditsUrl = "/credits"
                },
                UsageType.VideoCall => new
                {
                    error = "VideoCallNotAvailable",
                    plan = planName,
                    upgradeUrl = "/pricing"
                },
                _ => new
                {
                    error = "UsageLimitReached",
                    plan = planName,
                    upgradeUrl = "/pricing"
                }
            };
        }
    }
}
