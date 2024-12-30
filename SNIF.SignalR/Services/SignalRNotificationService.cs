using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SNIF.Core.DTOs;
using SNIF.Core.Interfaces;
using SNIF.SignalR.Hubs;

namespace SNIF.SignalR.Services
{
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<MatchHub> _hubContext;
        private readonly ILogger<SignalRNotificationService> _logger;

        public SignalRNotificationService(IHubContext<MatchHub> hubContext, ILogger<SignalRNotificationService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task NotifyNewMatch(string userId, MatchDto match)
        {
            try
            {
                _logger.LogInformation($"Notifying user {userId} of new match");
                await _hubContext.Clients.Group(userId)
                    .SendAsync("ReceiveNewMatch", match);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying user {userId} of new match");
                throw;
            }
        }

        public async Task NotifyMatchStatusUpdate(string userId, MatchDto match)
        {
            try
            {
                _logger.LogInformation($"Notifying user {userId} of match status update");
                await _hubContext.Clients.Group(userId)
                    .SendAsync("ReceiveMatchUpdate", match);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error notifying user {userId} of match status update");
                throw;
            }
        }
    }
}