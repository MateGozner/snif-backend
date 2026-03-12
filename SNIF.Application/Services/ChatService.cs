using AutoMapper;
using Microsoft.EntityFrameworkCore;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Specifications;
using SNIF.Core.Utilities;
using SNIF.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Busniess.Services
{
    public class ChatService : IChatService
    {
        private readonly IRepository<Message> _messageRepository;
        private readonly IRepository<MessageReaction> _reactionRepository;
        private readonly IMapper _mapper;

        public ChatService(IRepository<Message> messageRepository, IRepository<MessageReaction> reactionRepository, IMapper mapper)
        {
            _messageRepository = messageRepository;
            _reactionRepository = reactionRepository;
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

        public async Task<MessageDto?> GetMessageByIdAsync(string messageId)
        {
            var message = await _messageRepository.GetByIdAsync(messageId);
            return message == null ? null : _mapper.Map<MessageDto>(message);
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
                        PartnerProfilePicture = MediaPathResolver.ResolveProfilePicturePath(partner.ProfilePicturePath),
                        PartnerPetId = lastMessage.Match?.InitiatiorPet?.OwnerId == partnerId
                            ? lastMessage.Match.InitiatiorPet.Id
                            : lastMessage.Match?.TargetPet?.OwnerId == partnerId
                                ? lastMessage.Match.TargetPet.Id
                                : null,
                        PartnerPetName = lastMessage.Match?.InitiatiorPet?.OwnerId == partnerId
                            ? lastMessage.Match.InitiatiorPet.Name
                            : lastMessage.Match?.TargetPet?.OwnerId == partnerId
                                ? lastMessage.Match.TargetPet.Name
                                : null,
                        LastMessage = _mapper.Map<MessageDto>(lastMessage),
                        UnreadCount = g.Count(m => !m.IsRead && m.ReceiverId == userId)
                    };
                })
                .OrderByDescending(c => c.LastMessage?.CreatedAt);
        }

        private async Task<Message> GetAuthorizedMessageAsync(string messageId, string userId)
        {
            var message = await _messageRepository.GetByIdAsync(messageId);
            if (message == null)
                throw new KeyNotFoundException($"Message {messageId} not found");

            if (message.SenderId != userId && message.ReceiverId != userId)
                throw new UnauthorizedAccessException("User not authorized for this message");

            return message;
        }

        public async Task<MessageReactionDto> AddReactionAsync(string messageId, string userId, string emoji)
        {
            await GetAuthorizedMessageAsync(messageId, userId);

            // Check if reaction already exists
            var existing = (await _reactionRepository.FindAsync(
                r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji
            )).FirstOrDefault();

            if (existing != null)
                return _mapper.Map<MessageReactionDto>(existing);

            var reaction = new MessageReaction
            {
                Id = Guid.NewGuid().ToString(),
                MessageId = messageId,
                UserId = userId,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            try
            {
                await _reactionRepository.AddAsync(reaction);
            }
            catch (DbUpdateException)
            {
                // Race condition: another request inserted the same reaction between our check and insert.
                // Return the existing reaction instead of failing.
                var raceWinner = (await _reactionRepository.FindAsync(
                    r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji
                )).FirstOrDefault();

                if (raceWinner != null)
                    return _mapper.Map<MessageReactionDto>(raceWinner);

                throw;
            }

            return _mapper.Map<MessageReactionDto>(reaction);
        }

        public async Task<bool> RemoveReactionAsync(string messageId, string userId, string emoji)
        {
            await GetAuthorizedMessageAsync(messageId, userId);

            var existing = (await _reactionRepository.FindAsync(
                r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji
            )).FirstOrDefault();

            if (existing == null)
                return false;

            await _reactionRepository.DeleteAsync(existing);
            return true;
        }

        public async Task<MessageDto> SendImageMessageAsync(string matchId, string senderId, string receiverId, string attachmentUrl, string fileName, long sizeBytes)
        {
            var message = new Message
            {
                Id = Guid.NewGuid().ToString(),
                Content = "📷 Photo",
                SenderId = senderId,
                ReceiverId = receiverId,
                MatchId = matchId,
                IsRead = false,
                AttachmentUrl = attachmentUrl,
                AttachmentType = "image",
                AttachmentFileName = fileName,
                AttachmentSizeBytes = sizeBytes,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _messageRepository.AddAsync(message);
            return _mapper.Map<MessageDto>(message);
        }
    }
}
