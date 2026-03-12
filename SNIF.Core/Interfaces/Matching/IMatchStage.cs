using SNIF.Core.Models.Matching;

namespace SNIF.Core.Interfaces.Matching
{
    public interface IMatchStage
    {
        string Name { get; }
        Task<MatchPipelineContext> ExecuteAsync(MatchPipelineContext context);
    }
}
