using SNIF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.DTOs
{
    public record MatchDto
    {
        public string Id { get; init; } = null!;
        public PetDto InitiatorPet { get; init; } = null!;
        public PetDto TargetPet { get; init; } = null!;
        public PetPurpose MatchPurpose { get; init; }
        public MatchStatus Status { get; init; }
        public DateTime? ExpiresAt { get; init; }
    }

    public record UpdateMatchStatusDto
    {
        public MatchStatus Status { get; set; }
    }

    public record CreateMatchDto
    {
        public string InitiatorPetId { get; init; } = null!;
        public string TargetPetId { get; init; } = null!;
        public PetPurpose MatchPurpose { get; init; }
    }

    public record UpdateMatchDto
    {
        public MatchStatus Status { get; init; }
    }
}
