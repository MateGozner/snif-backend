using SNIF.Core.DTOs;

namespace SNIF.Core.Interfaces
{
    public interface IAdminService
    {
        // Dashboard
        Task<AdminDashboardDto> GetDashboardAsync();

        // User Management
        Task<AdminUserListResultDto> GetUsersAsync(AdminUserFilterDto filter);
        Task<AdminUserDetailDto> GetUserDetailAsync(string userId);
        Task SuspendUserAsync(string userId, int durationDays, string reason, string adminId);
        Task BanUserAsync(string userId, string reason, string adminId);
        Task UnsuspendUserAsync(string userId, string adminId);
        Task UnbanUserAsync(string userId, string adminId);
        Task WarnUserAsync(string userId, string adminId, string? reason);

        // Reports/Moderation
        Task<PagedResult<AdminReportDto>> GetReportsAsync(ReportFilterDto filter);
        Task ResolveReportAsync(string reportId, string resolution, string? notes, string adminId);
        Task DismissReportAsync(string reportId, string? notes, string adminId);

        // Subscriptions
        Task<AdminSubscriptionStatsDto> GetSubscriptionStatsAsync();
        Task<PagedResult<AdminSubscriptionDto>> GetSubscriptionsAsync(int page, int pageSize);

        // Revenue
        Task<List<RevenueDataPointDto>> GetRevenueAsync();

        // Payments
        Task<PagedResult<AdminPaymentTransactionDto>> GetPaymentTransactionsAsync(PaymentFilterDto filter);

        // System Health
        Task<SystemHealthDto> GetSystemHealthAsync();
    }
}
