using Microsoft.Extensions.Logging;
using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching
{
    public class RankingStage : IMatchStage
    {
        private readonly ILogger<RankingStage> _logger;

        public string Name => "Ranking";

        public RankingStage(ILogger<RankingStage> logger)
        {
            _logger = logger;
        }

        public Task<MatchPipelineContext> ExecuteAsync(MatchPipelineContext context)
        {
            var ranked = context.Candidates
                .Where(c => !c.IsFiltered)
                .OrderByDescending(c => c.Score)
                .Skip((context.Page - 1) * context.PageSize)
                .Take(context.PageSize)
                .ToList();

            // Replace candidates with the ranked page
            var filtered = context.Candidates.Where(c => c.IsFiltered).ToList();
            filtered.AddRange(ranked);
            context.Candidates = filtered;

            _logger.LogInformation(
                "Ranking complete: returning page {Page} with {Count} results",
                context.Page, ranked.Count);

            return Task.FromResult(context);
        }
    }
}
