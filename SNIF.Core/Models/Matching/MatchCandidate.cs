using SNIF.Core.Entities;
using SNIF.Core.Enums;

namespace SNIF.Core.Models.Matching
{
    public class MatchCandidate
    {
        public Pet Pet { get; set; } = null!;
        public double Score { get; set; }
        public double Distance { get; set; }
        public bool IsFiltered { get; set; }
        public RejectReason? RejectReason { get; set; }
        public Dictionary<string, double> ScoreBreakdown { get; set; } = new();
    }
}
