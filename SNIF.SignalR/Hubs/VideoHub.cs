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

        // Add dictionary to track call roles
        private static readonly Dictionary<string, (string CallerId, string ReceiverId)> _activeCallRoles
            = new Dictionary<string, (string CallerId, string ReceiverId)>();

        public VideoHub(ILogger<VideoHub> logger)
        {
            _logger = logger;
        }

        public async Task InitiateCall(string matchId, string receiverId)
        {
            var callerId = Context.UserIdentifier;
            var roomId = $"{RoomPrefix}{matchId}";

            // Store the caller and receiver roles
            _activeCallRoles[matchId] = (callerId, receiverId);

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.User(receiverId).SendAsync("IncomingCall", callerId, matchId);

            _logger.LogInformation($"Call initiated: Caller={callerId}, Receiver={receiverId}, Match={matchId}");
        }

        public async Task AcceptCall(string matchId, string callerId)
        {
            var receiverId = Context.UserIdentifier;
            var roomId = $"{RoomPrefix}{matchId}";

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            // Notify both parties that call was accepted
            await Clients.Group(roomId).SendAsync("CallAccepted", matchId);

            _logger.LogInformation($"Call accepted: Caller={callerId}, Receiver={receiverId}, Match={matchId}");
        }

        public async Task SendSignal(string matchId, string receiverId, string signal)
        {
            var userId = Context.UserIdentifier;

            _logger.LogInformation($"Signal sent: From={userId}, To={receiverId}, Match={matchId}");

            await Clients.User(receiverId).SendAsync("ReceiveSignal", signal);
        }

        public async Task EndCall(string matchId)
        {
            var userId = Context.UserIdentifier;
            var roomId = $"{RoomPrefix}{matchId}";

            await Clients.Group(roomId).SendAsync("CallEnded", matchId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

            _activeCallRoles.Remove(matchId);

            _logger.LogInformation($"Call ended: User={userId}, Match={matchId}");
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.UserIdentifier;

            // Find and end any active calls for this user
            var matchesToEnd = _activeCallRoles
                .Where(kvp => kvp.Value.CallerId == userId || kvp.Value.ReceiverId == userId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var matchId in matchesToEnd)
            {
                await EndCall(matchId);
            }

            _logger.LogInformation($"User {userId} disconnected from video hub");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
