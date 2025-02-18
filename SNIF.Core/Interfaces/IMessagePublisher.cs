
using SNIF.Core.Contracts;
using SNIF.Core.Entities;
using SNIF.Core.Models;

namespace SNIF.Core.Interfaces
{
    public interface IMessagePublisher
    {
        Task CreateWatchlistQueueForUser(string userId, UserPreferences preferences);
        Task PublishPetCreatedAsync(Pet pet);
        Task PublishMatchNotificationAsync(string userId, PetMatchNotification notification);

    }
}