using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace SNIF.SignalR.Hubs
{
    public class MatchHub : Hub
    {
        private readonly ILogger<MatchHub> _logger;

        public MatchHub(ILogger<MatchHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation($"Client Connected: {Context.ConnectionId}");
            _logger.LogInformation($"User ID: {Context.User?.Identity?.Name}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation($"Client Disconnected: {Context.ConnectionId}. Exception: {exception?.Message}");
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinUserGroup(string userId)
        {
            try
            {

                _logger.LogInformation($"Adding client {Context.ConnectionId} to group {userId}");
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
                _logger.LogInformation($"Successfully added to group {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding client {Context.ConnectionId} to group {userId}");
                throw;
            }
        }

        public async Task LeaveUserGroup(string userId)
        {
            try
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
                _logger.LogInformation($"Removed client {Context.ConnectionId} from group {userId}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error removing from group {userId}");
                throw;
            }
        }
    }
}