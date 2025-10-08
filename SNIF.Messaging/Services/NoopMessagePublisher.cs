using SNIF.Core.Contracts;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;

namespace SNIF.Messaging.Services
{
    // Fallback publisher that does nothing when RabbitMQ is disabled
    public class NoopMessagePublisher : IMessagePublisher
    {
        public Task CreateWatchlistQueueForUser(string userId, UserPreferences preferences)
            => Task.CompletedTask;

        public Task PublishMatchNotificationAsync(string userId, PetMatchNotification notification)
            => Task.CompletedTask;

        public Task PublishPetCreatedAsync(Pet pet)
            => Task.CompletedTask;
    }
}
