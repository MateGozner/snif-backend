using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.SignalR.Hubs
{
    public class VideoHub : Hub
    {
        private readonly ILogger<VideoHub> _logger;
        private const string RoomPrefix = "video_";
        private static readonly Dictionary<string, HashSet<string>> _matchConnections = new();
        private static readonly object _lock = new();

        public VideoHub(ILogger<VideoHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                lock (_lock)
                {
                    if (!_matchConnections.ContainsKey(userId))
                    {
                        _matchConnections[userId] = new HashSet<string>();
                    }
                    _matchConnections[userId].Add(Context.ConnectionId);
                }
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.UserIdentifier;
            if (!string.IsNullOrEmpty(userId))
            {
                lock (_lock)
                {
                    if (_matchConnections.ContainsKey(userId))
                    {
                        _matchConnections[userId].Remove(Context.ConnectionId);
                        if (!_matchConnections[userId].Any())
                        {
                            _matchConnections.Remove(userId);
                        }
                    }
                }
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task InitiateCall(string matchId, string receiverId)
        {
            var callerId = Context.UserIdentifier;
            if (string.IsNullOrEmpty(callerId) || string.IsNullOrEmpty(receiverId))
            {
                _logger.LogWarning("Invalid caller or receiver ID");
                return;
            }

            lock (_lock)
            {
                if (_matchConnections.TryGetValue(receiverId, out var connections))
                {
                    foreach (var connectionId in connections)
                    {
                        Groups.AddToGroupAsync(connectionId, $"{RoomPrefix}{matchId}");
                    }
                }
            }

            await Clients.User(receiverId).SendAsync("IncomingCall", callerId, matchId);
            _logger.LogInformation($"Call initiated from {callerId} to {receiverId} for match {matchId}");
        }

        public async Task AcceptCall(string matchId, string callerId)
        {
            var receiverId = Context.UserIdentifier;
            await Clients.Group($"{RoomPrefix}{matchId}").SendAsync("CallAccepted", matchId);
            _logger.LogInformation($"Call accepted by {receiverId} for match {matchId}");
        }

        public async Task DeclineCall(string matchId, string callerId)
        {
            var receiverId = Context.UserIdentifier;
            await Clients.Group($"{RoomPrefix}{matchId}").SendAsync("CallDeclined", matchId);
            _logger.LogInformation($"Call declined by {receiverId} for match {matchId}");
        }

        public async Task SendSignal(string matchId, string signal, string type)
        {
            var roomId = $"{RoomPrefix}{matchId}";
            await Clients.OthersInGroup(roomId).SendAsync("ReceiveSignal", signal, type);
            _logger.LogInformation($"Signal {type} sent in room {roomId}");
        }

        public async Task EndCall(string matchId)
        {
            var userId = Context.UserIdentifier;
            var roomId = $"{RoomPrefix}{matchId}";

            // Notify others in the group that the call has ended
            await Clients.OthersInGroup(roomId).SendAsync("CallEnded", matchId);

            // Remove connections from the group
            lock (_lock)
            {
                if (_matchConnections.TryGetValue(userId, out var connections))
                {
                    foreach (var connectionId in connections)
                    {
                        Groups.RemoveFromGroupAsync(connectionId, roomId);
                    }
                }
            }

            _logger.LogInformation($"Call ended by {userId} for match {matchId}");
        }




    }
}
