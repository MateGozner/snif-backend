using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching.ScoringFunctions
{
    public class ProfileCompletenessScorer : IMatchScoringFunction
    {
        private readonly ScoringWeights _weights;

        public string Name => "ProfileCompleteness";
        public double Weight => _weights.ProfileCompleteness;

        public ProfileCompletenessScorer(ScoringWeights weights)
        {
            _weights = weights;
        }

        public double Score(MatchCandidate candidate, MatchPipelineContext context)
        {
            var pet = candidate.Pet;
            int fields = 0;
            int filled = 0;

            // Name
            fields++;
            if (!string.IsNullOrEmpty(pet.Name)) filled++;

            // Species
            fields++;
            if (!string.IsNullOrEmpty(pet.Species)) filled++;

            // Breed
            fields++;
            if (!string.IsNullOrEmpty(pet.Breed)) filled++;

            // Age
            fields++;
            if (pet.Age > 0) filled++;

            // Location
            fields++;
            if (pet.Location != null) filled++;

            // Photos
            fields++;
            if (pet.Photos.Any()) filled++;

            // Purpose
            fields++;
            if (pet.Purpose.Any()) filled++;

            // Personality
            fields++;
            if (pet.Personality.Any()) filled++;

            // Medical history
            fields++;
            if (pet.MedicalHistory != null) filled++;

            return fields > 0 ? (double)filled / fields : 0.0;
        }
    }
}
