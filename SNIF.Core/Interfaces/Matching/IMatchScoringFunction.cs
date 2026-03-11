using SNIF.Core.Models.Matching;

namespace SNIF.Core.Interfaces.Matching
{
    public interface IMatchScoringFunction
    {
        string Name { get; }
        double Weight { get; }
        double Score(MatchCandidate candidate, MatchPipelineContext context);
    }
}
