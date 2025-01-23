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
    [Route("api/[controller]")]
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

        [HttpGet("{matchId}")]
        [ProducesResponseType(typeof(IEnumerable<MessageDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<MessageDto>>> GetMatchMessages(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
                return BadRequest(new ErrorResponse { Message = "Match ID is required" });

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
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse { Message = "An error occurred while retrieving messages" });
            }
        }

        [HttpGet("{userId}/chats")]
        [ProducesResponseType(typeof(IEnumerable<ChatSummaryDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<IEnumerable<ChatSummaryDto>>> GetUserChats(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest(new ErrorResponse { Message = "User ID is required" });

            var authUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(authUserId) || authUserId != userId)
                return Unauthorized(new ErrorResponse { Message = "Unauthorized access" });

            try
            {
                var chats = await _chatService.GetUserChatsAsync(userId);
                return Ok(chats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ErrorResponse { Message = "An error occurred while retrieving chats" });
            }
        }


        [HttpPost("{messageId}/read")]
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
