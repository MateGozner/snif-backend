using AutoMapper;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Specifications;
using SNIF.Infrastructure.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Busniess.Services
{
    public class MatchService : IMatchService
    {
        private readonly IRepository<Match> _matchRepository;
        private readonly IRepository<Pet> _petRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IMapper _mapper;

        public MatchService(IRepository<Match> matchRepository, IRepository<Pet> petRepository, IRepository<User> userRepository, IMapper mapper)
        {
            _matchRepository = matchRepository;
            _petRepository = petRepository;
            _userRepository = userRepository;
            _mapper = mapper;
        }


        public async Task<MatchDto> CreateMatchAsync(string initiatorPetId, CreateMatchDto createMatchDto)
        {
            var match = new Match
            {
                Id = Guid.NewGuid().ToString(),
                InitiatiorPetId = initiatorPetId,
                TargetPetId = createMatchDto.TargetPetId,
                Purpose = createMatchDto.MatchPurpose,
                Status = MatchStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            await _matchRepository.AddAsync(match);
            return _mapper.Map<MatchDto>(match);
        }

        public async Task DeleteMatchAsync(string matchId)
        {
            var match = await _matchRepository.GetByIdAsync(matchId)
                ?? throw new KeyNotFoundException("Match not found");

            await _matchRepository.DeleteAsync(match);
        }

        public async Task<MatchDto> GetMatchByIdAsync(string matchId)
        {
            var match = await _matchRepository.GetBySpecificationAsync(
                new MatchWithDetailsSpecification(matchId))
                ?? throw new KeyNotFoundException("Match not found");

            return _mapper.Map<MatchDto>(match);
        }

        public async Task<IEnumerable<MatchDto>> GetPetMatchesAsync(string petId)
        {
            var matches = await _matchRepository.FindBySpecificationAsync(
                new MatchWithDetailsSpecification(m =>
                    m.InitiatiorPetId == petId ||
                    m.TargetPetId == petId));

            return _mapper.Map<IEnumerable<MatchDto>>(matches);
        }

        public async Task<IEnumerable<PetDto>> GetPotentialMatchesAsync(string petId, PetPurpose purpose)
        {
            var initiatorPet = await _petRepository.GetBySpecificationAsync(
                new PetWithDetailsSpecification(petId))
                ?? throw new KeyNotFoundException("Pet not found");

            var initiatorOwner = await _userRepository.GetBySpecificationAsync(
                new UserWithDetailsSpecification(initiatorPet.OwnerId))
                ?? throw new KeyNotFoundException("Pet owner not found");

            // Get existing matches for the initiator pet
            var existingMatches = await _matchRepository.FindBySpecificationAsync(
                new MatchWithDetailsSpecification(m =>
                    (m.InitiatiorPetId == petId || m.TargetPetId == petId) &&
                    (m.Status == MatchStatus.Pending || m.Status == MatchStatus.Accepted)));

            // Get matched pet IDs
            var matchedPetIds = existingMatches.SelectMany(m => new[] { m.InitiatiorPetId, m.TargetPetId })
                                              .Distinct()
                                              .ToList();

            // Get all pets except the initiator and already matched pets
            var allPets = await _petRepository.FindBySpecificationAsync(
                new PetWithDetailsSpecification(p =>
                    p.Id != petId &&
                    !matchedPetIds.Contains(p.Id)));

            var matches = allPets
                .Where(targetPet =>
                {
                    if (targetPet.Location == null || initiatorPet.Location == null)
                        return false;

                    // 1. Distance filter
                    var distance = CalculateDistance(
                        initiatorPet.Location.Latitude,
                        initiatorPet.Location.Longitude,
                        targetPet.Location.Latitude,
                        targetPet.Location.Longitude);

                    if (distance > initiatorOwner.Preferences!.SearchRadius)
                        return false;

                    // 2. Species and breed filter
                    if (initiatorPet.Species != targetPet.Species)
                        return false;

                    // 3. Gender filter (for breeding)
                    if (purpose == PetPurpose.Breeding && initiatorPet.Gender == targetPet.Gender)
                        return false;

                    return targetPet.Purpose.Contains(purpose);
                })
                .OrderBy(targetPet => CalculateDistance(
                    initiatorPet.Location!.Latitude,
                    initiatorPet.Location.Longitude,
                    targetPet.Location!.Latitude,
                    targetPet.Location.Longitude));

            return _mapper.Map<IEnumerable<PetDto>>(matches);
        }

        public async Task<MatchDto> UpdateMatchStatusAsync(string matchId, MatchStatus status)
        {
            var match = await _matchRepository.GetByIdAsync(matchId)
                ?? throw new KeyNotFoundException("Match not found");

            match.Status = status;
            match.UpdatedAt = DateTime.UtcNow;

            await _matchRepository.UpdateAsync(match);
            return _mapper.Map<MatchDto>(match);
        }

        public async Task<IEnumerable<MatchDto>> GetPendingMatchesForPetAsync(string petId)
        {
            var pendingMatches = await _matchRepository.FindBySpecificationAsync(
                new MatchWithDetailsSpecification(m =>
                    m.TargetPetId == petId &&
                    m.Status == MatchStatus.Pending));

            // Order by newest first
            var orderedMatches = pendingMatches
                .OrderByDescending(m => m.CreatedAt);

            return _mapper.Map<IEnumerable<MatchDto>>(orderedMatches);
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
