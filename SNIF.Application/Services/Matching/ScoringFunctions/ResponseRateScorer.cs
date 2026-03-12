using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching.ScoringFunctions
{
    public class ResponseRateScorer : IMatchScoringFunction
    {
        private readonly ScoringWeights _weights;

        public string Name => "Engagement";
        public double Weight => _weights.Engagement;

        public ResponseRateScorer(ScoringWeights weights)
        {
            _weights = weights;
        }

        public double Score(MatchCandidate candidate, MatchPipelineContext context)
        {
            var owner = candidate.Pet.Owner;
            if (owner == null)
                return 0.5;

            // Recency of last seen as a proxy for engagement
            if (owner.LastSeen.HasValue)
            {
                var hoursSinceLastSeen = (DateTime.UtcNow - owner.LastSeen.Value).TotalHours;

                // Last seen within 24h → 1.0; decays over 30 days
                if (hoursSinceLastSeen <= 24) return 1.0;
                if (hoursSinceLastSeen <= 72) return 0.8;
                if (hoursSinceLastSeen <= 168) return 0.6; // 1 week
                if (hoursSinceLastSeen <= 720) return 0.3; // 30 days
                return 0.1;
            }

            // Online right now
            if (owner.IsOnline) return 1.0;

            return 0.5;
        }
    }
}
