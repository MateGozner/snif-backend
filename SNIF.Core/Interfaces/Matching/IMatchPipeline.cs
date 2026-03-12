using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Models.Matching;

namespace SNIF.Core.Interfaces.Matching
{
    public interface IMatchPipeline
    {
        Task<List<MatchCandidate>> ExecuteAsync(
            Pet sourcePet,
            IEnumerable<Pet> allPets,
            User owner,
            HashSet<string> existingMatchPetIds,
            PetPurpose? purposeFilter = null,
            int page = 1,
            int pageSize = 20);
    }
}
