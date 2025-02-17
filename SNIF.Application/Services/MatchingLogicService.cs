using Microsoft.Extensions.Logging;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Busniess.Services
{
    public class MatchingLogicService : IMatchingLogicService
    {
        private readonly ILogger<MatchingLogicService> _logger;
        private const double DefaultSearchRadius = 50.0;

        public MatchingLogicService(ILogger<MatchingLogicService> logger)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<(Pet Pet, double Distance)>> FindPotentialMatches(
            Pet sourcePet,
            IEnumerable<Pet> potentialMatches,
            User ownerWithPreferences,
            PetPurpose? purposeFilter = null)
        {
            _logger.LogInformation(
                "Finding potential matches for pet {PetId} with purpose filter {Purpose}",
                sourcePet.Id,
                purposeFilter);

            var matches = new List<(Pet Pet, double Distance)>();
            double searchRadius = ownerWithPreferences.Preferences?.SearchRadius ?? DefaultSearchRadius;

            foreach (var targetPet in potentialMatches)
            {
                if (!IsPotentialMatch(sourcePet, targetPet, ownerWithPreferences, purposeFilter))
                    continue;

                var distance = CalculateDistance(
                    sourcePet.Location!.Latitude,
                    sourcePet.Location.Longitude,
                    targetPet.Location!.Latitude,
                    targetPet.Location.Longitude);

                if (distance <= searchRadius)
                {
                    matches.Add((targetPet, distance));
                }
            }

            return matches;
        }

        private bool IsPotentialMatch(
            Pet sourcePet,
            Pet targetPet,
            User ownerWithPreferences,
            PetPurpose? purposeFilter)
        {
            // Basic validation
            if (targetPet.Location == null || sourcePet.Location == null)
                return false;

            if (targetPet.Id == sourcePet.Id)
                return false;

            // Species match
            if (sourcePet.Species != targetPet.Species)
                return false;

            // Purpose compatibility
            if (purposeFilter.HasValue)
            {
                // For specific purpose filter
                if (!targetPet.Purpose.Contains(purposeFilter.Value))
                    return false;

                // Breeding specific check
                if (purposeFilter.Value == PetPurpose.Breeding && sourcePet.Gender == targetPet.Gender)
                    return false;
            }
            else
            {
                // Check if pets share any purpose
                var sharedPurposes = sourcePet.Purpose.Intersect(targetPet.Purpose);
                if (!sharedPurposes.Any())
                    return false;

                // Breeding check for any shared breeding purpose
                if (sharedPurposes.Contains(PetPurpose.Breeding) && sourcePet.Gender == targetPet.Gender)
                    return false;
            }

            return true;
        }

        private static double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Earth radius in kilometers

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
