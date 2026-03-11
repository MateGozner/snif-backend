using SNIF.Core.Enums;

namespace SNIF.Core.Models
{
    public class PlanLimits
    {
        public int MaxPets { get; set; }
        public int DailyLikes { get; set; }
        public int DailySuperSniffs { get; set; }
        public int SearchRadiusKm { get; set; }
        public bool VideoCallEnabled { get; set; }
        public bool HasAds { get; set; }
        public bool UnlimitedLikes { get; set; }

        public static readonly Dictionary<SubscriptionPlan, PlanLimits> Plans = new()
        {
            [SubscriptionPlan.Free] = new PlanLimits
            {
                MaxPets = 1,
                DailyLikes = 5,
                DailySuperSniffs = 1,
                SearchRadiusKm = 5,
                VideoCallEnabled = false,
                HasAds = true,
                UnlimitedLikes = false
            },
            [SubscriptionPlan.GoodBoy] = new PlanLimits
            {
                MaxPets = 3,
                DailyLikes = 25,
                DailySuperSniffs = 5,
                SearchRadiusKm = 50,
                VideoCallEnabled = true,
                HasAds = false,
                UnlimitedLikes = false
            },
            [SubscriptionPlan.AlphaPack] = new PlanLimits
            {
                MaxPets = 5,
                DailyLikes = int.MaxValue,
                DailySuperSniffs = int.MaxValue,
                SearchRadiusKm = 500,
                VideoCallEnabled = true,
                HasAds = false,
                UnlimitedLikes = true
            },
            [SubscriptionPlan.TreatBag] = new PlanLimits
            {
                MaxPets = 1,
                DailyLikes = 10,
                DailySuperSniffs = 10,
                SearchRadiusKm = 25,
                VideoCallEnabled = false,
                HasAds = true,
                UnlimitedLikes = false
            }
        };

        public static PlanLimits GetLimits(SubscriptionPlan plan)
        {
            return Plans.TryGetValue(plan, out var limits) ? limits : Plans[SubscriptionPlan.Free];
        }
    }
}
