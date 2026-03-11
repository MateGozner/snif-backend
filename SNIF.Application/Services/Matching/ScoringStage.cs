using Microsoft.Extensions.Logging;
using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching
{
    public class ScoringStage : IMatchStage
    {
        private readonly IEnumerable<IMatchScoringFunction> _scorers;
        private readonly ILogger<ScoringStage> _logger;

        public string Name => "Scoring";

        public ScoringStage(IEnumerable<IMatchScoringFunction> scorers, ILogger<ScoringStage> logger)
        {
            _scorers = scorers;
            _logger = logger;
        }

        public Task<MatchPipelineContext> ExecuteAsync(MatchPipelineContext context)
        {
            foreach (var candidate in context.Candidates)
            {
                if (candidate.IsFiltered) continue;

                double totalScore = 0;

                foreach (var scorer in _scorers)
                {
                    var raw = Math.Clamp(scorer.Score(candidate, context), 0.0, 1.0);
                    var weighted = raw * scorer.Weight;
                    candidate.ScoreBreakdown[scorer.Name] = raw;
                    totalScore += weighted;
                }

                candidate.Score = totalScore;
            }

            _logger.LogInformation("Scoring complete for {Count} candidates",
                context.Candidates.Count(c => !c.IsFiltered));

            return Task.FromResult(context);
        }
    }
}
