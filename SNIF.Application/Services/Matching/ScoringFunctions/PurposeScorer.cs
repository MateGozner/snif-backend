using SNIF.Core.Enums;
using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching.ScoringFunctions
{
    public class PurposeScorer : IMatchScoringFunction
    {
        private readonly ScoringWeights _weights;

        public string Name => "Purpose";
        public double Weight => _weights.Purpose;

        public PurposeScorer(ScoringWeights weights)
        {
            _weights = weights;
        }

        public double Score(MatchCandidate candidate, MatchPipelineContext context)
        {
            var sourcePurposes = context.SourcePet.Purpose;
            var targetPurposes = candidate.Pet.Purpose;

            if (!sourcePurposes.Any() || !targetPurposes.Any())
                return 0.0;

            // If a specific purpose filter is set, check direct match
            if (context.PurposeFilter.HasValue)
            {
                return targetPurposes.Contains(context.PurposeFilter.Value) ? 1.0 : 0.0;
            }

            // Jaccard-like overlap of purpose sets
            var intersection = sourcePurposes.Intersect(targetPurposes).Count();
            var union = sourcePurposes.Union(targetPurposes).Count();

            return union > 0 ? (double)intersection / union : 0.0;
        }
    }
}
