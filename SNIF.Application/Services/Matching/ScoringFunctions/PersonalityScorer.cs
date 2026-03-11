using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching.ScoringFunctions
{
    public class PersonalityScorer : IMatchScoringFunction
    {
        private readonly ScoringWeights _weights;

        public string Name => "Personality";
        public double Weight => _weights.Personality;

        public PersonalityScorer(ScoringWeights weights)
        {
            _weights = weights;
        }

        public double Score(MatchCandidate candidate, MatchPipelineContext context)
        {
            var sourceTraits = context.SourcePet.Personality;
            var targetTraits = candidate.Pet.Personality;

            if (sourceTraits == null || targetTraits == null ||
                !sourceTraits.Any() || !targetTraits.Any())
                return 0.0;

            // Jaccard similarity: |A ∩ B| / |A ∪ B|
            var sourceSet = new HashSet<string>(sourceTraits, StringComparer.OrdinalIgnoreCase);
            var targetSet = new HashSet<string>(targetTraits, StringComparer.OrdinalIgnoreCase);

            var intersection = sourceSet.Intersect(targetSet).Count();
            var union = sourceSet.Union(targetSet).Count();

            return union > 0 ? (double)intersection / union : 0.0;
        }
    }
}
