using SNIF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Entities
{
    public class Match : BaseEntity
    {
        public string InitiatiorPetId { get; set; } = null!;
        public virtual Pet InitiatiorPet { get; set; } = null!;

        public string TargetPetId { get; set; } = null!;
        public virtual Pet TargetPet { get; set; } = null!;

        public PetPurpose Purpose { get; set; }
        public MatchStatus Status { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
