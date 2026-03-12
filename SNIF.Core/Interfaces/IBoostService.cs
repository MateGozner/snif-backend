using SNIF.Core.DTOs;
using SNIF.Core.Enums;

namespace SNIF.Core.Interfaces
{
    public interface IBoostService
    {
        Task<BoostPurchaseResultDto> PurchaseWithCredits(string userId, BoostType boostType, int durationDays);
        Task<BoostDto> ActivateFromOrder(string userId, BoostType boostType, int durationDays, string orderId);
        Task<IReadOnlyList<BoostDto>> GetActiveBoosts(string userId);
        Task<AvailableBoostsDto> GetAvailableBoosts(string userId);
        Task<bool> HasActiveBoost(string userId, BoostType boostType);
    }
}
