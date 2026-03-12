using SNIF.Core.DTOs;

namespace SNIF.Core.Interfaces
{
    public interface IEntitlementService
    {
        Task<EntitlementSnapshotDto> GetEntitlementAsync(string userId);
        Task EnsurePetCanUsePremiumActionsAsync(string userId, string petId, string actionName);
        bool IsPetLocked(EntitlementSnapshotDto entitlement, string petId);
    }
}