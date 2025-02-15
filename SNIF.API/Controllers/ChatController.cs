using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Client;
using SNIF.API.Extensions;
using SNIF.Busniess.Services;
using SNIF.Core.DTOs;
using SNIF.Core.Interfaces;
using System.Security.Claims;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/chats")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly IMatchService _matchService;

        public ChatController(IChatService chatService, IMatchService matchService)
        {
            _chatService = chatService;
            _matchService = matchService;
        }

        // GET api/chats/matches/{matchId}/messages
        [HttpGet("matches/{matchId}/messages")]
        [ProducesResponseType(typeof(IEnumerable<MessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMatchMessages(string matchId)
        {
            var authUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(authUserId))
                return Unauthorized(new ErrorResponse { Message = "User not authenticated" });

            try
            {
                var match = await _matchService.GetMatchByIdAsync(matchId);
                if (match.InitiatorPet.OwnerId != authUserId && match.TargetPet.OwnerId != authUserId)
                    return Unauthorized(new ErrorResponse { Message = "User not authorized to view these messages" });

                var messages = await _chatService.GetMatchMessagesAsync(matchId);
                return Ok(messages);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Match not found" });
            }
        }

        // GET api/chats/users/{userId}
        [HttpGet("users/{userId}")]
        [ProducesResponseType(typeof(IEnumerable<ChatSummaryDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<ChatSummaryDto>>> GetUserChats(string userId)
        {
            var authUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(authUserId) || authUserId != userId)
                return Unauthorized(new ErrorResponse { Message = "Unauthorized access" });

            try
            {
                var chats = await _chatService.GetUserChatsAsync(userId);
                return Ok(chats);
            }
            catch (Exception)
            {
                throw;
            }
        }


        [HttpPatch("messages/{messageId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult> MarkAsRead(string messageId)
        {
            if (string.IsNullOrEmpty(messageId))
                return BadRequest(new ErrorResponse { Message = "Message ID is required" });

            try
            {
                await _chatService.MarkAsReadAsync(messageId);
                return Ok();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Message not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse { Message = "An error occurred while marking message as read" });
            }
        }
    }
}
