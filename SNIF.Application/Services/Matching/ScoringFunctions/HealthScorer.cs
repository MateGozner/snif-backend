using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching.ScoringFunctions
{
    public class HealthScorer : IMatchScoringFunction
    {
        private readonly ScoringWeights _weights;

        public string Name => "Health";
        public double Weight => _weights.Health;

        public HealthScorer(ScoringWeights weights)
        {
            _weights = weights;
        }

        public double Score(MatchCandidate candidate, MatchPipelineContext context)
        {
            var medical = candidate.Pet.MedicalHistory;
            if (medical == null)
                return 0.3; // No medical info → low score

            double score = 0.0;

            // Vaccinated
            if (medical.IsVaccinated)
                score += 0.4;

            // Has vaccination records
            if (medical.VaccinationRecords.Any())
                score += 0.2;

            // Recent checkup (within last year)
            if (medical.LastCheckup.HasValue)
            {
                var daysSinceCheckup = (DateTime.UtcNow - medical.LastCheckup.Value).TotalDays;
                if (daysSinceCheckup <= 365) score += 0.2;
                else if (daysSinceCheckup <= 730) score += 0.1;
            }

            // No health issues
            if (!medical.HealthIssues.Any())
                score += 0.2;

            return Math.Min(score, 1.0);
        }
    }
}
