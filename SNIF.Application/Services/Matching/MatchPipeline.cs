using Microsoft.Extensions.Logging;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching
{
    public class MatchPipeline : IMatchPipeline
    {
        private readonly IEnumerable<IMatchStage> _stages;
        private readonly ILogger<MatchPipeline> _logger;

        public MatchPipeline(IEnumerable<IMatchStage> stages, ILogger<MatchPipeline> logger)
        {
            _stages = stages;
            _logger = logger;
        }

        public async Task<List<MatchCandidate>> ExecuteAsync(
            Pet sourcePet,
            IEnumerable<Pet> allPets,
            User owner,
            HashSet<string> existingMatchPetIds,
            PetPurpose? purposeFilter = null,
            int page = 1,
            int pageSize = 20)
        {
            var context = new MatchPipelineContext
            {
                SourcePet = sourcePet,
                Owner = owner,
                PurposeFilter = purposeFilter,
                Page = page,
                PageSize = pageSize,
                ExistingMatchPetIds = existingMatchPetIds,
                Candidates = allPets.Select(p => new MatchCandidate { Pet = p }).ToList()
            };

            _logger.LogInformation(
                "Starting match pipeline for pet {PetId} with {Count} candidates",
                sourcePet.Id, context.Candidates.Count);

            foreach (var stage in _stages)
            {
                context = await stage.ExecuteAsync(context);
                _logger.LogInformation(
                    "Stage {Stage} complete: {Count} candidates remaining",
                    stage.Name, context.Candidates.Count(c => !c.IsFiltered));
            }

            return context.Candidates.Where(c => !c.IsFiltered).ToList();
        }
    }
}
