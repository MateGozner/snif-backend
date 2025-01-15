using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.DTOs
{
    public record MessageDto
    {
        public string Id { get; init; } = null!;
        public string Content { get; init; } = null!;
        public string SenderId { get; init; } = null!;
        public string ReceiverId { get; init; } = null!;
        public string MatchId { get; init; } = null!;
        public bool IsRead { get; init; }
        public DateTime CreatedAt { get; init; }
    }

    public record CreateMessageDto
    {
        public required string Content { get; init; }
        public required string ReceiverId { get; init; }
        public required string MatchId { get; init; }
    }

    public record ChatSummaryDto
    {
        public string MatchId { get; init; } = null!;
        public string PartnerId { get; init; } = null!;
        public string PartnerName { get; init; } = null!;
        public MessageDto? LastMessage { get; init; }
        public int UnreadCount { get; init; }
    }

}
