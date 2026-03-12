using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching.ScoringFunctions
{
    public class FreshnessScorer : IMatchScoringFunction
    {
        private readonly ScoringWeights _weights;

        public string Name => "Freshness";
        public double Weight => _weights.Freshness;

        public FreshnessScorer(ScoringWeights weights)
        {
            _weights = weights;
        }

        public double Score(MatchCandidate candidate, MatchPipelineContext context)
        {
            var pet = candidate.Pet;
            var profileAge = (DateTime.UtcNow - pet.CreatedAt).TotalDays;

            // Newer profiles score higher; decay over 180 days
            if (profileAge <= 7) return 1.0;
            if (profileAge <= 30) return 0.8;
            if (profileAge <= 90) return 0.6;
            if (profileAge <= 180) return 0.4;
            return 0.2;
        }
    }
}
