using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SNIF.Core.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.SignalR.Hubs
{
    public class OnlineHub : Hub
    {
        private readonly IUserService _userService;
        private readonly ILogger<OnlineHub> _logger;
        private static readonly ConcurrentDictionary<string, HashSet<string>> _userConnections = new();

        public OnlineHub(IUserService userService, ILogger<OnlineHub> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            _logger.LogInformation($"User {userId} connected");

            if (userId != null)
            {
                var connectionId = Context.ConnectionId;

                // Add connection to tracking
                _userConnections.AddOrUpdate(
                    userId,
                    new HashSet<string> { connectionId },
                    (_, connections) =>
                    {
                        connections.Add(connectionId);
                        return connections;
                    });

                await Groups.AddToGroupAsync(connectionId, userId);

                // Only update status if this is the first connection for this user
                if (_userConnections[userId].Count == 1)
                {
                    await _userService.UpdateUserOnlineStatus(userId, true);
                    await Clients.Others.SendAsync("UserOnline", userId);
                    _logger.LogInformation($"User {userId} is now online");
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
            {
                var connectionId = Context.ConnectionId;

                // Remove this connection
                if (_userConnections.TryGetValue(userId, out var connections))
                {
                    connections.Remove(connectionId);

                    // If this was the last connection for this user
                    if (connections.Count == 0)
                    {
                        _userConnections.TryRemove(userId, out _);
                        await _userService.UpdateUserOnlineStatus(userId, false);
                        await Clients.Others.SendAsync("UserOffline", userId);
                        _logger.LogInformation($"User {userId} is now offline");
                    }
                }

                await Groups.RemoveFromGroupAsync(connectionId, userId);
            }

            await base.OnDisconnectedAsync(exception);
        }
    }
}
