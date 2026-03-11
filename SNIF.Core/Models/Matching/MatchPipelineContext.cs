using SNIF.Core.Entities;
using SNIF.Core.Enums;

namespace SNIF.Core.Models.Matching
{
    public class MatchPipelineContext
    {
        public Pet SourcePet { get; set; } = null!;
        public User Owner { get; set; } = null!;
        public PetPurpose? PurposeFilter { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public double EffectiveSearchRadiusKm { get; set; }
        public List<MatchCandidate> Candidates { get; set; } = new();
        public HashSet<string> ExistingMatchPetIds { get; set; } = new();
    }
}
