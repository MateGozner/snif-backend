using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching.ScoringFunctions
{
    public class BreedScorer : IMatchScoringFunction
    {
        private readonly ScoringWeights _weights;

        public string Name => "Breed";
        public double Weight => _weights.Breed;

        public BreedScorer(ScoringWeights weights)
        {
            _weights = weights;
        }

        public double Score(MatchCandidate candidate, MatchPipelineContext context)
        {
            var sourceBreed = context.SourcePet.Breed;
            var targetBreed = candidate.Pet.Breed;

            if (string.IsNullOrEmpty(sourceBreed) || string.IsNullOrEmpty(targetBreed))
                return 0.5;

            // Exact breed match
            if (string.Equals(sourceBreed, targetBreed, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            // Same species but different breed
            if (string.Equals(context.SourcePet.Species, candidate.Pet.Species, StringComparison.OrdinalIgnoreCase))
                return 0.5;

            // Different species
            return 0.2;
        }
    }
}
