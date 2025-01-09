using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SNIF.Core.Interfaces;
using SNIF.SignalR.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.SignalR.Services
{
    public class VideoService : IVideoService
    {
        private readonly IHubContext<VideoHub> _hubContext;
        private readonly ILogger<VideoService> _logger;

        public VideoService(IHubContext<VideoHub> hubContext, ILogger<VideoService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task InitiateVideoCall(string matchId, string callerId, string receiverId)
        {
            await _hubContext.Clients.User(receiverId)
                .SendAsync("VideoCallInitiated", callerId, matchId);
            _logger.LogInformation($"Video call initiated from {callerId} to {receiverId} for match {matchId}");
        }

        public async Task EndVideoCall(string matchId, string userId)
        {
            await _hubContext.Clients.Group($"video_{matchId}")
                .SendAsync("VideoCallEnded", userId);
            _logger.LogInformation($"Video call ended by {userId} for match {matchId}");
        }
    }
}
