using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SNIF.Core.Constants;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Infrastructure.Data;
using System.Diagnostics;

namespace SNIF.Busniess.Services
{
    public class AdminService : IAdminService
    {
        private readonly SNIFContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IEntitlementService _entitlementService;
        private readonly IPaymentService _paymentService;
        private static readonly DateTime _startTime = DateTime.UtcNow;

        public AdminService(
            SNIFContext context,
            UserManager<User> userManager,
            IEntitlementService entitlementService,
            IPaymentService paymentService)
        {
            _context = context;
            _userManager = userManager;
            _entitlementService = entitlementService;
            _paymentService = paymentService;
        }

        public async Task<AdminDashboardDto> GetDashboardAsync()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var weekAgo = today.AddDays(-7);
            var thirtyDaysAgo = today.AddDays(-30);

            var totalUsers = await _context.Users.CountAsync();
            var totalPets = await _context.Pets.CountAsync();
            var totalMatches = await _context.Matches.CountAsync();

            var activeSubscriptions = await _context.Subscriptions
                .CountAsync(s => s.Status == SubscriptionStatus.Active && s.PlanId != SubscriptionPlan.Free);

            var newUsersToday = await _context.Users
                .CountAsync(u => u.CreatedAt >= today);

            var newUsersThisWeek = await _context.Users
                .CountAsync(u => u.CreatedAt >= weekAgo);

            // Match rate: accepted / total * 100
            var acceptedMatches = await _context.Matches
                .CountAsync(m => m.Status == MatchStatus.Accepted);
            var matchRate = totalMatches > 0
                ? (double)acceptedMatches / totalMatches * 100
                : 0;

            // MRR calculation
            var goodBoyCount = await _context.Subscriptions
                .CountAsync(s => s.PlanId == SubscriptionPlan.GoodBoy && s.Status == SubscriptionStatus.Active);
            var alphaPackCount = await _context.Subscriptions
                .CountAsync(s => s.PlanId == SubscriptionPlan.AlphaPack && s.Status == SubscriptionStatus.Active);
            var revenueThisMonth = goodBoyCount * 4.99m + alphaPackCount * 9.99m;

            // Top breeds
            var topBreeds = await _context.Pets
                .GroupBy(p => p.Breed)
                .Select(g => new BreedStatDto { Breed = g.Key, Count = g.Count() })
                .OrderByDescending(b => b.Count)
                .Take(10)
                .ToListAsync();

            // User growth last 30 days
            var userGrowth = await _context.Users
                .Where(u => u.CreatedAt >= thirtyDaysAgo)
                .GroupBy(u => u.CreatedAt.Date)
                .Select(g => new DailyStatDto { Date = g.Key, Count = g.Count() })
                .OrderBy(d => d.Date)
                .ToListAsync();

            // Matches over time last 30 days
            var matchesOverTime = await _context.Matches
                .Where(m => m.CreatedAt >= thirtyDaysAgo)
                .GroupBy(m => m.CreatedAt.Date)
                .Select(g => new DailyStatDto { Date = g.Key, Count = g.Count() })
                .OrderBy(d => d.Date)
                .ToListAsync();

            return new AdminDashboardDto
            {
                TotalUsers = totalUsers,
                TotalPets = totalPets,
                TotalMatches = totalMatches,
                ActiveSubscriptions = activeSubscriptions,
                RevenueThisMonth = revenueThisMonth,
                NewUsersToday = newUsersToday,
                NewUsersThisWeek = newUsersThisWeek,
                MatchRate = matchRate,
                TopBreeds = topBreeds,
                UserGrowth = userGrowth,
                MatchesOverTime = matchesOverTime
            };
        }

        public async Task<AdminUserListResultDto> GetUsersAsync(AdminUserFilterDto filter)
        {
            var query = _context.Users.AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(u =>
                    u.Name.ToLower().Contains(search) ||
                    (u.Email != null && u.Email.ToLower().Contains(search)));
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(filter.Status))
            {
                query = filter.Status.ToLower() switch
                {
                    "banned" => query.Where(u => u.IsBanned),
                    "suspended" => query.Where(u => u.SuspendedUntil != null && u.SuspendedUntil > DateTime.UtcNow),
                    "active" => query.Where(u => !u.IsBanned && (u.SuspendedUntil == null || u.SuspendedUntil <= DateTime.UtcNow)),
                    _ => query
                };
            }

            // Role filtering: query UserRoles join
            if (!string.IsNullOrWhiteSpace(filter.Role))
            {
                var roleId = await _context.Roles
                    .Where(r => r.Name == filter.Role)
                    .Select(r => r.Id)
                    .FirstOrDefaultAsync();

                if (roleId != null)
                {
                    var userIdsInRole = _context.UserRoles
                        .Where(ur => ur.RoleId == roleId)
                        .Select(ur => ur.UserId);
                    query = query.Where(u => userIdsInRole.Contains(u.Id));
                }
                else
                {
                    // No matching role, return empty
                    query = query.Where(u => false);
                }
            }

            // Sorting
            query = filter.SortBy?.ToLower() switch
            {
                "name" => filter.SortDirection?.ToLower() == "desc"
                    ? query.OrderByDescending(u => u.Name)
                    : query.OrderBy(u => u.Name),
                "email" => filter.SortDirection?.ToLower() == "desc"
                    ? query.OrderByDescending(u => u.Email)
                    : query.OrderBy(u => u.Email),
                "createdat" => filter.SortDirection?.ToLower() == "desc"
                    ? query.OrderByDescending(u => u.CreatedAt)
                    : query.OrderBy(u => u.CreatedAt),
                _ => query.OrderByDescending(u => u.CreatedAt)
            };

            var candidates = await query
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    Email = u.Email ?? "",
                    PetCount = u.Pets.Count,
                    u.IsOnline,
                    u.IsBanned,
                    u.SuspendedUntil,
                    u.CreatedAt
                })
                .ToListAsync();

            var userIds = candidates.Select(candidate => candidate.Id).ToList();
            var roleMap = await BuildRoleMapAsync(userIds);
            var latestSubscriptionMap = await BuildLatestSubscriptionMapAsync(userIds);
            var users = new List<AdminUserDto>(candidates.Count);

            foreach (var candidate in candidates)
            {
                latestSubscriptionMap.TryGetValue(candidate.Id, out var latestSubscription);
                var entitlement = await _entitlementService.GetEntitlementAsync(candidate.Id);
                var flags = BuildSupportFlags(entitlement, latestSubscription);

                roleMap.TryGetValue(candidate.Id, out var role);

                users.Add(new AdminUserDto
                {
                    Id = candidate.Id,
                    Name = candidate.Name,
                    Email = candidate.Email,
                    Role = role,
                    PetCount = candidate.PetCount,
                    IsOnline = candidate.IsOnline,
                    IsBanned = candidate.IsBanned,
                    SuspendedUntil = candidate.SuspendedUntil,
                    CreatedAt = candidate.CreatedAt,
                    SubscriptionPlan = entitlement.BillingPlan == SubscriptionPlan.Free
                        ? null
                        : entitlement.BillingPlan.ToString(),
                    SupportFlags = flags
                });
            }

            var filteredUsers = ApplySupportIssueFilter(users, filter.SupportIssue);
            var totalCount = filteredUsers.Count;
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

            return new AdminUserListResultDto
            {
                Items = filteredUsers
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                SupportSummary = BuildSupportSummary(filteredUsers)
            };
        }

        public async Task<AdminUserDetailDto> GetUserDetailAsync(string userId)
        {
            var user = await _context.Users
                .Include(u => u.Pets)
                .FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new KeyNotFoundException("User not found");

            var roles = await _userManager.GetRolesAsync(user);

            var matchesCount = await _context.Matches
                .CountAsync(m =>
                    m.InitiatiorPet.OwnerId == userId ||
                    m.TargetPet.OwnerId == userId);

            var reportsAgainstCount = await _context.Reports
                .CountAsync(r => r.TargetUserId == userId);

            SubscriptionActivationStatusDto? activationStatus = null;
            try
            {
                activationStatus = await _paymentService.RefreshSubscriptionActivationStatus(userId);
            }
            catch
            {
                activationStatus = null;
            }

            var entitlement = await _entitlementService.GetEntitlementAsync(userId);

            var latestSubscription = await _context.Subscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.UpdatedAt)
                .ThenByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            var subscriptionSupport = latestSubscription == null
                && entitlement.BillingPlan == SubscriptionPlan.Free
                && activationStatus?.State == null
                ? null
                : new AdminSubscriptionSupportDto
                {
                    BillingPlan = entitlement.BillingPlan,
                    EffectivePlan = entitlement.EffectivePlan,
                    SubscriptionStatus = entitlement.SubscriptionStatus,
                    EffectiveStatus = entitlement.EffectiveStatus,
                    ActivationState = activationStatus?.State,
                    ActivationMessage = activationStatus?.Message,
                    CurrentPeriodStart = entitlement.CurrentPeriodStart,
                    CurrentPeriodEnd = entitlement.CurrentPeriodEnd,
                    CancelAtPeriodEnd = entitlement.CancelAtPeriodEnd,
                    DowngradeEffectiveAt = entitlement.DowngradeEffectiveAt,
                    IsOverPetLimit = entitlement.IsOverPetLimit,
                    LockedPetCount = entitlement.LockedPets,
                    LockedPets = entitlement.PetStates.Where(p => p.IsLocked).ToList(),
                    PaymentProviderSubscriptionId = latestSubscription?.PaymentProviderSubscriptionId,
                    PaymentProviderCustomerId = latestSubscription?.PaymentProviderCustomerId,
                    PaidButStillFree = entitlement.BillingPlan != SubscriptionPlan.Free
                        && entitlement.EffectivePlan == SubscriptionPlan.Free
                };

            return new AdminUserDetailDto
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault(),
                PetCount = user.Pets.Count,
                IsOnline = user.IsOnline,
                IsBanned = user.IsBanned,
                SuspendedUntil = user.SuspendedUntil,
                CreatedAt = user.CreatedAt,
                SubscriptionPlan = subscriptionSupport?.BillingPlan.ToString(),
                Pets = user.Pets.Select(p => new PetSummaryDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Species = p.Species,
                    Breed = p.Breed
                }).ToList(),
                MatchesCount = matchesCount,
                ReportsCount = reportsAgainstCount,
                SubscriptionSupport = subscriptionSupport
            };
        }

        private async Task<Dictionary<string, string?>> BuildRoleMapAsync(List<string> userIds)
        {
            if (userIds.Count == 0)
            {
                return new Dictionary<string, string?>();
            }

            var userRoles = await _context.UserRoles
                .Where(ur => userIds.Contains(ur.UserId))
                .Join(_context.Roles, ur => ur.RoleId, role => role.Id, (ur, role) => new { ur.UserId, RoleName = role.Name })
                .ToListAsync();

            return userRoles
                .GroupBy(entry => entry.UserId)
                .ToDictionary(group => group.Key, group => group.Select(entry => entry.RoleName).FirstOrDefault());
        }

        private async Task<Dictionary<string, Subscription>> BuildLatestSubscriptionMapAsync(List<string> userIds)
        {
            if (userIds.Count == 0)
            {
                return new Dictionary<string, Subscription>();
            }

            var subscriptions = await _context.Subscriptions
                .AsNoTracking()
                .Where(subscription => userIds.Contains(subscription.UserId))
                .OrderByDescending(subscription => subscription.UpdatedAt)
                .ThenByDescending(subscription => subscription.CreatedAt)
                .ToListAsync();

            return subscriptions
                .GroupBy(subscription => subscription.UserId)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private static AdminUserSupportFlagsDto BuildSupportFlags(
            EntitlementSnapshotDto entitlement,
            Subscription? latestSubscription)
        {
            var pendingActivation = latestSubscription != null
                && latestSubscription.PlanId != SubscriptionPlan.Free
                && latestSubscription.Status != SubscriptionStatus.Active
                && latestSubscription.Status != SubscriptionStatus.Trialing
                && latestSubscription.Status != SubscriptionStatus.PastDue;

            var paidButStillFree = entitlement.BillingPlan != SubscriptionPlan.Free
                && entitlement.EffectivePlan == SubscriptionPlan.Free;
            var pastDue = entitlement.SubscriptionStatus == SubscriptionStatus.PastDue
                || entitlement.EffectiveStatus == EntitlementStatus.PastDueGrace;
            var cancelAtPeriodEnd = entitlement.CancelAtPeriodEnd;
            var downgradeOrLockedPets = entitlement.EffectiveStatus == EntitlementStatus.Downgrading
                || entitlement.EffectiveStatus == EntitlementStatus.Downgraded
                || entitlement.IsOverPetLimit
                || entitlement.LockedPets > 0;

            var issueCount = 0;
            if (pendingActivation) issueCount++;
            if (paidButStillFree) issueCount++;
            if (pastDue) issueCount++;
            if (cancelAtPeriodEnd) issueCount++;
            if (downgradeOrLockedPets) issueCount++;

            return new AdminUserSupportFlagsDto
            {
                PendingActivation = pendingActivation,
                PaidButStillFree = paidButStillFree,
                PastDue = pastDue,
                CancelAtPeriodEnd = cancelAtPeriodEnd,
                DowngradeOrLockedPets = downgradeOrLockedPets,
                IssueCount = issueCount
            };
        }

        private static List<AdminUserDto> ApplySupportIssueFilter(List<AdminUserDto> users, string? supportIssue)
        {
            if (string.IsNullOrWhiteSpace(supportIssue) || string.Equals(supportIssue, "all", StringComparison.OrdinalIgnoreCase))
            {
                return users;
            }

            return users
                .Where(user => MatchesSupportIssue(user.SupportFlags, supportIssue))
                .ToList();
        }

        private static bool MatchesSupportIssue(AdminUserSupportFlagsDto flags, string supportIssue)
        {
            return supportIssue.ToLowerInvariant() switch
            {
                "any" => flags.IssueCount > 0,
                "pendingactivation" => flags.PendingActivation,
                "paidbutstillfree" => flags.PaidButStillFree,
                "pastdue" => flags.PastDue,
                "cancelatperiodend" => flags.CancelAtPeriodEnd,
                "downgradelockedpets" => flags.DowngradeOrLockedPets,
                _ => true
            };
        }

        private static AdminUserSupportSummaryDto BuildSupportSummary(List<AdminUserDto> users)
        {
            return new AdminUserSupportSummaryDto
            {
                FlaggedUsers = users.Count(user => user.SupportFlags.IssueCount > 0),
                PendingActivation = users.Count(user => user.SupportFlags.PendingActivation),
                PaidButStillFree = users.Count(user => user.SupportFlags.PaidButStillFree),
                PastDue = users.Count(user => user.SupportFlags.PastDue),
                CancelAtPeriodEnd = users.Count(user => user.SupportFlags.CancelAtPeriodEnd),
                DowngradeOrLockedPets = users.Count(user => user.SupportFlags.DowngradeOrLockedPets)
            };
        }

        public async Task SuspendUserAsync(string userId, int durationDays, string reason, string adminId)
        {
            if (durationDays < 1)
                throw new InvalidOperationException("Suspension duration must be at least 1 day.");

            await ValidateAdminActionAsync(userId, adminId);

            var user = await _context.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("User not found");

            user.SuspendedUntil = DateTime.UtcNow.AddDays(durationDays);
            user.BanReason = reason;
            await _context.SaveChangesAsync();
        }

        public async Task BanUserAsync(string userId, string reason, string adminId)
        {
            await ValidateAdminActionAsync(userId, adminId);

            var user = await _context.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("User not found");

            user.IsBanned = true;
            user.BanReason = reason;
            await _context.SaveChangesAsync();
        }

        private async Task ValidateAdminActionAsync(string targetUserId, string adminId)
        {
            var targetUser = await _userManager.FindByIdAsync(targetUserId)
                ?? throw new InvalidOperationException("User not found.");
            var adminUser = await _userManager.FindByIdAsync(adminId)
                ?? throw new InvalidOperationException("Admin not found.");

            var targetRoles = await _userManager.GetRolesAsync(targetUser);
            var adminRoles = await _userManager.GetRolesAsync(adminUser);

            int GetRank(IList<string> roles)
            {
                if (roles.Contains(AppRoles.SuperAdmin)) return 4;
                if (roles.Contains(AppRoles.Admin)) return 3;
                if (roles.Contains(AppRoles.Moderator)) return 2;
                if (roles.Contains(AppRoles.Support)) return 1;
                return 0;
            }

            if (GetRank(targetRoles) >= GetRank(adminRoles))
                throw new InvalidOperationException("Cannot perform action on a user with equal or higher rank.");
        }

        public async Task UnsuspendUserAsync(string userId, string adminId)
        {
            var user = await _context.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("User not found");

            user.IsBanned = false;
            user.SuspendedUntil = null;
            user.BanReason = null;
            await _context.SaveChangesAsync();
        }

        public async Task UnbanUserAsync(string userId, string adminId)
        {
            await ValidateAdminActionAsync(userId, adminId);

            var user = await _context.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("User not found");

            user.IsBanned = false;
            user.SuspendedUntil = null;
            user.BanReason = null;
            await _context.SaveChangesAsync();
        }

        public async Task<PagedResult<AdminReportDto>> GetReportsAsync(ReportFilterDto filter)
        {
            var query = _context.Reports
                .Include(r => r.Reporter)
                .Include(r => r.TargetUser)
                .Include(r => r.TargetPet)
                .AsQueryable();

            if (filter.Status.HasValue)
                query = query.Where(r => r.Status == filter.Status.Value);

            if (filter.Reason.HasValue)
                query = query.Where(r => r.Reason == filter.Reason.Value);

            query = query.OrderByDescending(r => r.CreatedAt);

            var totalCount = await query.CountAsync();
            var page = Math.Max(1, filter.Page);
            var pageSize = Math.Clamp(filter.PageSize, 1, 100);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var reports = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => new AdminReportDto
                {
                    Id = r.Id,
                    ReporterName = r.Reporter.Name,
                    TargetUserId = r.TargetUserId,
                    TargetUserName = r.TargetUser.Name,
                    TargetPetName = r.TargetPet != null ? r.TargetPet.Name : null,
                    Reason = r.Reason,
                    Description = r.Description,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return new PagedResult<AdminReportDto>
            {
                Items = reports,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task ResolveReportAsync(string reportId, string resolution, string? notes, string adminId)
        {
            var report = await _context.Reports.FindAsync(reportId)
                ?? throw new KeyNotFoundException("Report not found");

            report.Status = ReportStatus.Resolved;
            report.Resolution = resolution;
            report.Notes = notes;
            report.ReviewedBy = adminId;
            report.ReviewedAt = DateTime.UtcNow;
            report.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task DismissReportAsync(string reportId, string? notes, string adminId)
        {
            var report = await _context.Reports.FindAsync(reportId)
                ?? throw new KeyNotFoundException("Report not found");

            report.Status = ReportStatus.Dismissed;
            report.Notes = notes;
            report.ReviewedBy = adminId;
            report.ReviewedAt = DateTime.UtcNow;
            report.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<AdminSubscriptionStatsDto> GetSubscriptionStatsAsync()
        {
            var activeSubscriptions = await _context.Subscriptions
                .Where(s => s.Status == SubscriptionStatus.Active)
                .GroupBy(s => s.PlanId)
                .Select(g => new { Plan = g.Key, Count = g.Count() })
                .ToListAsync();

            var totalFree = activeSubscriptions
                .FirstOrDefault(s => s.Plan == SubscriptionPlan.Free)?.Count ?? 0;
            var totalGoodBoy = activeSubscriptions
                .FirstOrDefault(s => s.Plan == SubscriptionPlan.GoodBoy)?.Count ?? 0;
            var totalAlphaPack = activeSubscriptions
                .FirstOrDefault(s => s.Plan == SubscriptionPlan.AlphaPack)?.Count ?? 0;
            var totalTreatBag = activeSubscriptions
                .FirstOrDefault(s => s.Plan == SubscriptionPlan.TreatBag)?.Count ?? 0;

            var mrr = totalGoodBoy * 4.99m + totalAlphaPack * 9.99m;

            return new AdminSubscriptionStatsDto
            {
                TotalFree = totalFree,
                TotalGoodBoy = totalGoodBoy,
                TotalAlphaPack = totalAlphaPack,
                TotalTreatBag = totalTreatBag,
                Mrr = mrr
            };
        }

        public async Task<SystemHealthDto> GetSystemHealthAsync()
        {
            var dbConnected = await _context.Database.CanConnectAsync();

            var lastMigration = (await _context.Database
                .GetAppliedMigrationsAsync())
                .LastOrDefault();

            var uptime = DateTime.UtcNow - _startTime;

            return new SystemHealthDto
            {
                DbConnected = dbConnected,
                RedisConnected = false, // Redis not configured yet
                ActiveSignalRConnections = 0, // Would need IHubContext to track
                Uptime = uptime,
                LastMigration = lastMigration
            };
        }

        public async Task WarnUserAsync(string userId, string adminId, string? reason)
        {
            await ValidateAdminActionAsync(userId, adminId);

            var user = await _context.Users.FindAsync(userId)
                ?? throw new KeyNotFoundException("User not found");

            user.WarningCount++;
            await _context.SaveChangesAsync();
        }

        public async Task<PagedResult<AdminSubscriptionDto>> GetSubscriptionsAsync(int page, int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var query = _context.Subscriptions
                .Include(s => s.User)
                .OrderByDescending(s => s.CurrentPeriodStart);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new AdminSubscriptionDto
                {
                    UserId = s.UserId,
                    UserName = s.User.Name,
                    Plan = s.PlanId.ToString(),
                    Status = s.Status.ToString(),
                    StartDate = s.CurrentPeriodStart,
                    EndDate = s.CurrentPeriodEnd,
                    Amount = s.PlanId == SubscriptionPlan.GoodBoy ? 4.99m
                           : s.PlanId == SubscriptionPlan.AlphaPack ? 9.99m
                           : 0m
                })
                .ToListAsync();

            return new PagedResult<AdminSubscriptionDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<List<RevenueDataPointDto>> GetRevenueAsync()
        {
            var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);

            var monthlyData = await _context.Subscriptions
                .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue)
                .Where(s => s.CurrentPeriodStart >= twelveMonthsAgo)
                .GroupBy(s => new { s.CurrentPeriodStart.Year, s.CurrentPeriodStart.Month })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    Count = g.Count(),
                    GoodBoy = g.Count(s => s.PlanId == SubscriptionPlan.GoodBoy),
                    AlphaPack = g.Count(s => s.PlanId == SubscriptionPlan.AlphaPack)
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            return monthlyData.Select(m => new RevenueDataPointDto
            {
                Month = $"{m.Year}-{m.Month:D2}",
                Revenue = m.GoodBoy * 4.99m + m.AlphaPack * 9.99m,
                Subscriptions = m.Count
            }).ToList();
        }
    }
}
