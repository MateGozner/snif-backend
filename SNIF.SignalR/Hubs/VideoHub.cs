using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Infrastructure.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.SignalR.Hubs
{
    [Authorize]
    public class VideoHub : Hub
    {
        private readonly ILogger<VideoHub> _logger;
        private readonly SNIFContext _context;
        private readonly IMatchService _matchService;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IUsageService _usageService;
        private const string RoomPrefix = "video_";

        private static readonly ConcurrentDictionary<string, (string CallerId, string ReceiverId, DateTime StartedAt)> _activeCallRoles
            = new ConcurrentDictionary<string, (string CallerId, string ReceiverId, DateTime StartedAt)>();

        public VideoHub(ILogger<VideoHub> logger, SNIFContext context, IMatchService matchService, IPushNotificationService pushNotificationService, IUsageService usageService)
        {
            _logger = logger;
            _context = context;
            _matchService = matchService;
            _pushNotificationService = pushNotificationService;
            _usageService = usageService;
        }

        private async Task<string> ResolvePeerUserIdAsync(string matchId, string userId)
        {
            try
            {
                return await _matchService.GetPeerUserIdAsync(matchId, userId);
            }
            catch (KeyNotFoundException)
            {
                throw new HubException("Match not found");
            }
            catch (UnauthorizedAccessException)
            {
                throw new HubException("User not authorized for this match");
            }
        }

        public async Task InitiateCall(string matchId, string receiverId)
        {
            var callerId = Context.UserIdentifier
                ?? throw new HubException("User not authenticated");
            _ = receiverId;

            var usageResult = await _usageService.CanPerformAction(callerId, UsageType.VideoCall);
            if (!usageResult.Allowed)
            {
                throw new HubException("Video calls require Good Boy or Alpha Pack subscription");
            }

            var resolvedReceiverId = await ResolvePeerUserIdAsync(matchId, callerId);

            var roomId = $"{RoomPrefix}{matchId}";

            // Store the caller and receiver roles
            _activeCallRoles[matchId] = (callerId, resolvedReceiverId, DateTime.MinValue);

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);
            await Clients.User(resolvedReceiverId).SendAsync("IncomingCall", callerId, matchId);

            _logger.LogInformation($"Call initiated: Caller={callerId}, Receiver={resolvedReceiverId}, Match={matchId}");
        }

        public async Task AcceptCall(string matchId, string callerId)
        {
            var receiverId = Context.UserIdentifier
                ?? throw new HubException("User not authenticated");
            _ = callerId;
            var resolvedCallerId = await ResolvePeerUserIdAsync(matchId, receiverId);
            var roomId = $"{RoomPrefix}{matchId}";

            // Record call start time when accepted (atomic to prevent race condition)
            _activeCallRoles.AddOrUpdate(matchId,
                _ => (receiverId, resolvedCallerId, DateTime.UtcNow),
                (_, roles) => (roles.CallerId, roles.ReceiverId, DateTime.UtcNow));

            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            // Notify both parties that call was accepted
            await Clients.Group(roomId).SendAsync("CallAccepted", matchId);

            _logger.LogInformation($"Call accepted: Caller={resolvedCallerId}, Receiver={receiverId}, Match={matchId}");
        }

        public async Task SendSignal(string matchId, string receiverId, string signal)
        {
            var userId = Context.UserIdentifier
                ?? throw new HubException("User not authenticated");
            _ = receiverId;
            var resolvedReceiverId = await ResolvePeerUserIdAsync(matchId, userId);

            _logger.LogInformation($"Signal sent: From={userId}, To={resolvedReceiverId}, Match={matchId}");

            await Clients.User(resolvedReceiverId).SendAsync("ReceiveSignal", signal);
        }

        public async Task EndCall(string matchId)
        {
            var userId = Context.UserIdentifier;
            var roomId = $"{RoomPrefix}{matchId}";

            int durationSeconds = 0;
            string? otherUserId = null;

            if (_activeCallRoles.TryRemove(matchId, out var call))
            {
                var endedAt = DateTime.UtcNow;
                durationSeconds = call.StartedAt > DateTime.MinValue
                    ? (int)(endedAt - call.StartedAt).TotalSeconds
                    : 0;

                // Determine who the other user is for push notification
                otherUserId = userId == call.CallerId ? call.ReceiverId : call.CallerId;

                // Persist the call record to the database
                var videoCall = new VideoCall
                {
                    Id = Guid.NewGuid().ToString(),
                    MatchId = matchId,
                    CallerUserId = call.CallerId,
                    ReceiverUserId = call.ReceiverId,
                    StartedAt = call.StartedAt,
                    EndedAt = endedAt,
                    DurationSeconds = durationSeconds,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.VideoCalls.Add(videoCall);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"VideoCall saved: Match={matchId}, Duration={durationSeconds}s");
            }

            await Clients.Group(roomId).SendAsync("CallEnded", matchId, durationSeconds);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);

            // Send push notification to the other user about call end
            if (otherUserId != null)
            {
                var durationText = durationSeconds >= 60
                    ? $"{durationSeconds / 60}m {durationSeconds % 60}s"
                    : $"{durationSeconds}s";

                await _pushNotificationService.SendPushAsync(
                    otherUserId,
                    "Call Ended \ud83d\udcf5",
                    $"Video call ended. Duration: {durationText}",
                    new Dictionary<string, string>
                    {
                        ["type"] = "video_call_ended",
                        ["matchId"] = matchId,
                        ["duration"] = durationSeconds.ToString()
                    });
            }

            _logger.LogInformation($"Call ended: User={userId}, Match={matchId}, Duration={durationSeconds}s");
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
