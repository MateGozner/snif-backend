namespace SNIF.Core.Configuration
{
    public class LemonSqueezyOptions
    {
        public const string SectionName = "LemonSqueezy";
        public string ApiKey { get; set; } = string.Empty;
        public string StoreId { get; set; } = string.Empty;
        public string SigningSecret { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = "https://api.lemonsqueezy.com";
        public LemonSqueezyVariants Variants { get; set; } = new();
    }

    public class LemonSqueezyVariants
    {
        public string GoodBoyMonthly { get; set; } = string.Empty;
        public string GoodBoyYearly { get; set; } = string.Empty;
        public string AlphaPackMonthly { get; set; } = string.Empty;
        public string AlphaPackYearly { get; set; } = string.Empty;
        public string TreatBag10 { get; set; } = string.Empty;
        public string TreatBag50 { get; set; } = string.Empty;
        public string TreatBag100 { get; set; } = string.Empty;
        public string DayPassRadius50_1 { get; set; } = string.Empty;
        public string DayPassRadius50_3 { get; set; } = string.Empty;
        public string DayPassRadius50_7 { get; set; } = string.Empty;
        public string DayPassVideoChat_1 { get; set; } = string.Empty;
        public string DayPassVideoChat_3 { get; set; } = string.Empty;
        public string DayPassVideoChat_7 { get; set; } = string.Empty;
    }
}
