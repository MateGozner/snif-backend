using SNIF.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Interfaces
{
    public interface IChatService
    {
        Task<MessageDto> SendMessageAsync(CreateMessageDto messageDto, string senderId);
        Task<IEnumerable<MessageDto>> GetMatchMessagesAsync(string matchId);
        Task<MessageDto> MarkAsReadAsync(string messageId);
        Task<IEnumerable<ChatSummaryDto>> GetUserChatsAsync(string userId);
    }
}
