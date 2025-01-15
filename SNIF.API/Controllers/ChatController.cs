using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using SNIF.Core.DTOs;
using SNIF.Core.Interfaces;
using System.Security.Claims;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;

        public ChatController(IChatService chatService)
        {
            _chatService = chatService;
        }

        [HttpGet("{matchId}")]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMatchMessages(string matchId)
        {
            var messages = await _chatService.GetMatchMessagesAsync(matchId);
            return Ok(messages);
        }

        [HttpGet("{userId}/chats")]
        public async Task<ActionResult<IEnumerable<ChatSummaryDto>>> GetUserChats(string userId)
        {
            var authUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (authUserId != userId)
            {
                return Unauthorized();
            }

            var chats = await _chatService.GetUserChatsAsync(userId);
            return Ok(chats);
        }

        [HttpPost("{messageId}/read")]
        public async Task<ActionResult> MarkAsRead(string messageId)
        {
            await _chatService.MarkAsReadAsync(messageId);
            return Ok();
        }
    }
}
