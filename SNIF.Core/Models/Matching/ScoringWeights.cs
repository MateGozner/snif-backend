namespace SNIF.Core.Models.Matching
{
    public class ScoringWeights
    {
        public double Distance { get; set; } = 0.25;
        public double Purpose { get; set; } = 0.20;
        public double Breed { get; set; } = 0.15;
        public double Personality { get; set; } = 0.15;
        public double ProfileCompleteness { get; set; } = 0.08;
        public double Health { get; set; } = 0.07;
        public double Engagement { get; set; } = 0.05;
        public double Freshness { get; set; } = 0.05;
    }
}
