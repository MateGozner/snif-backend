using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using SNIF.Core.DTOs;
using SNIF.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.SignalR.Hubs
{
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatHub> _logger;

        public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        public async Task SendMessage(string matchId, string receiverId, string content)
        {
            var senderId = Context.UserIdentifier!;
            var message = await _chatService.SendMessageAsync(new CreateMessageDto
            {
                Content = content,
                ReceiverId = receiverId,
                MatchId = matchId
            }, senderId);

            await Clients.Users(new[] { senderId, receiverId })
                .SendAsync("ReceiveMessage", message);
        }

        public async Task MarkMessageAsRead(string messageId)
        {
            var userId = Context.UserIdentifier!;
            await _chatService.MarkAsReadAsync(messageId);

            // Notify both users about the read status
            await Clients.Users(new[] { userId })
                .SendAsync("MessageRead", messageId);
        }

        public async Task JoinChat(string matchId)
        {
            var chatRoomId = $"chat_{matchId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, chatRoomId);
            _logger.LogInformation($"User {Context.UserIdentifier} joined chat room {chatRoomId}");
        }

        public async Task LeaveChat(string matchId)
        {
            var chatRoomId = $"chat_{matchId}";
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatRoomId);
            _logger.LogInformation($"User {Context.UserIdentifier} left chat room {chatRoomId}");
        }
    }
}
