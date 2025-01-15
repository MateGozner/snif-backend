using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Specifications;
using SNIF.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Busniess.Services
{
    public class ChatService : IChatService
    {
        private readonly IRepository<Message> _messageRepository;
        private readonly IMapper _mapper;

        public ChatService(IRepository<Message> messageRepository, IMapper mapper)
        {
            _messageRepository = messageRepository;
            _mapper = mapper;
        }

        public async Task<MessageDto> SendMessageAsync(CreateMessageDto messageDto, string senderId)
        {
            var message = _mapper.Map<Message>(messageDto);
            message.SenderId = senderId;

            await _messageRepository.AddAsync(message);
            return _mapper.Map<MessageDto>(message);
        }

        public async Task<IEnumerable<MessageDto>> GetMatchMessagesAsync(string matchId)
        {
            var messages = await _messageRepository.FindBySpecificationAsync(
                new MessageSpecification(matchId));
            return _mapper.Map<IEnumerable<MessageDto>>(messages);
        }

        public async Task<MessageDto> MarkAsReadAsync(string messageId)
        {
            var message = await _messageRepository.GetByIdAsync(messageId);
            if (message != null && !message.IsRead)
            {
                message.IsRead = true;
                await _messageRepository.UpdateAsync(message);
                return _mapper.Map<MessageDto>(message);
            }
            return null;

        }
        public async Task<IEnumerable<ChatSummaryDto>> GetUserChatsAsync(string userId)
        {
            var messages = await _messageRepository.FindBySpecificationAsync(
                new ChatSummarySpecification(userId));
            return messages
                .GroupBy(m => m.MatchId)
                .Select(g =>
                {
                    var lastMessage = g.OrderByDescending(m => m.CreatedAt).First();
                    var partnerId = lastMessage.SenderId == userId ?
                        lastMessage.ReceiverId : lastMessage.SenderId;
                    var partner = lastMessage.SenderId == userId ?
                        lastMessage.Receiver : lastMessage.Sender;

                    return new ChatSummaryDto
                    {
                        MatchId = g.Key,
                        PartnerId = partnerId,
                        PartnerName = partner.Name,
                        LastMessage = _mapper.Map<MessageDto>(lastMessage),
                        UnreadCount = g.Count(m => !m.IsRead && m.ReceiverId == userId)
                    };
                })
                .OrderByDescending(c => c.LastMessage?.CreatedAt);
        }
    }
}
