using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching.ScoringFunctions
{
    public class DistanceScorer : IMatchScoringFunction
    {
        private readonly ScoringWeights _weights;

        public string Name => "Distance";
        public double Weight => _weights.Distance;

        public DistanceScorer(ScoringWeights weights)
        {
            _weights = weights;
        }

        public double Score(MatchCandidate candidate, MatchPipelineContext context)
        {
            double searchRadius = context.EffectiveSearchRadiusKm > 0
                ? context.EffectiveSearchRadiusKm
                : context.Owner.Preferences?.SearchRadius ?? 50.0;
            if (searchRadius <= 0) return 1.0;

            // Closer = higher score. At 0km → 1.0, at searchRadius → 0.0
            return Math.Max(0.0, 1.0 - (candidate.Distance / searchRadius));
        }
    }
}
