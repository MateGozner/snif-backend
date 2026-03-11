using Microsoft.Extensions.Logging;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models;
using SNIF.Core.Models.Matching;

namespace SNIF.Busniess.Services.Matching
{
    public class HardFilterStage : IMatchStage
    {
        private readonly ILogger<HardFilterStage> _logger;
        private readonly IEntitlementService _entitlementService;
        private const double DefaultSearchRadius = 50.0;

        public string Name => "HardFilter";

        public HardFilterStage(ILogger<HardFilterStage> logger, IEntitlementService entitlementService)
        {
            _logger = logger;
            _entitlementService = entitlementService;
        }

        public async Task<MatchPipelineContext> ExecuteAsync(MatchPipelineContext context)
        {
            var source = context.SourcePet;
            var prefs = source.DiscoveryPreferences;
            double searchRadius = context.Owner.Preferences?.SearchRadius ?? DefaultSearchRadius;

            var ownerEntitlement = await _entitlementService.GetEntitlementAsync(context.Owner.Id);
            searchRadius = Math.Min(searchRadius, ownerEntitlement.Limits.SearchRadiusKm);
            context.EffectiveSearchRadiusKm = searchRadius;

            foreach (var candidate in context.Candidates)
            {
                if (candidate.IsFiltered) continue;

                var target = candidate.Pet;
                var targetEntitlement = await _entitlementService.GetEntitlementAsync(target.OwnerId);

                if (_entitlementService.IsPetLocked(targetEntitlement, target.Id))
                {
                    Reject(candidate, RejectReason.LockedByEntitlement);
                    continue;
                }

                // HF-1: Self-pet exclusion
                if (target.Id == source.Id)
                {
                    Reject(candidate, RejectReason.SelfPet);
                    continue;
                }

                // HF-2: Same-owner exclusion
                if (target.OwnerId == source.OwnerId)
                {
                    Reject(candidate, RejectReason.SameOwner);
                    continue;
                }

                // HF-3: Species filter
                if (source.Species != target.Species)
                {
                    if (prefs == null || !prefs.AllowOtherSpecies)
                    {
                        Reject(candidate, RejectReason.SpeciesMismatch);
                        continue;
                    }
                }

                // HF-4: Breed filter
                if (prefs != null && !prefs.AllowOtherBreeds)
                {
                    if (source.Breed != target.Breed)
                    {
                        Reject(candidate, RejectReason.BreedMismatch);
                        continue;
                    }
                }

                // HF-5: Age range filter
                if (prefs?.MinAge != null && target.Age < prefs.MinAge)
                {
                    Reject(candidate, RejectReason.AgeBelowMin);
                    continue;
                }
                if (prefs?.MaxAge != null && target.Age > prefs.MaxAge)
                {
                    Reject(candidate, RejectReason.AgeAboveMax);
                    continue;
                }

                // HF-6: Gender preference filter
                if (prefs?.PreferredGender != null)
                {
                    if (Enum.TryParse<Gender>(prefs.PreferredGender, true, out var preferredGender))
                    {
                        if (target.Gender != preferredGender)
                        {
                            Reject(candidate, RejectReason.GenderPreference);
                            continue;
                        }
                    }
                }

                // HF-7: Breeding gender guard
                if (context.PurposeFilter == PetPurpose.Breeding && source.Gender == target.Gender)
                {
                    Reject(candidate, RejectReason.BreedingGenderGuard);
                    continue;
                }

                // HF-8: Distance radius (Haversine)
                if (source.Location == null || target.Location == null)
                {
                    Reject(candidate, RejectReason.OutOfRange);
                    continue;
                }

                var distance = HaversineDistance(
                    source.Location.Latitude, source.Location.Longitude,
                    target.Location.Latitude, target.Location.Longitude);
                candidate.Distance = distance;

                if (distance > searchRadius)
                {
                    Reject(candidate, RejectReason.OutOfRange);
                    continue;
                }

                // HF-9: Existing match exclusion
                if (context.ExistingMatchPetIds.Contains(target.Id))
                {
                    Reject(candidate, RejectReason.ExistingMatch);
                    continue;
                }
            }

            _logger.LogInformation(
                "HardFilter: {Passed} passed, {Filtered} filtered out of {Total}",
                context.Candidates.Count(c => !c.IsFiltered),
                context.Candidates.Count(c => c.IsFiltered),
                context.Candidates.Count);

            return context;
        }

        private static void Reject(MatchCandidate candidate, RejectReason reason)
        {
            candidate.IsFiltered = true;
            candidate.RejectReason = reason;
        }

        private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // Earth radius in km
            var dLat = ToRad(lat2 - lat1);
            var dLon = ToRad(lon2 - lon1);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double ToRad(double degrees) => degrees * Math.PI / 180;
    }
}
