using AutoMapper;
using SNIF.Core.Contracts;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Specifications;
using SNIF.Infrastructure.Repository;

using System.Threading.Tasks;

namespace SNIF.Busniess.Services
{
    public class MatchService : IMatchService
    {
        private readonly IRepository<Match> _matchRepository;
        private readonly IRepository<Pet> _petRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IMapper _mapper;
        private readonly INotificationService _notificationService;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IMatchingLogicService _matchingLogic;

        public MatchService(IRepository<Match> matchRepository, IRepository<Pet> petRepository, IRepository<User> userRepository, IMapper mapper, INotificationService notificationService, IMessagePublisher messagePublisher, IMatchingLogicService matchingLogic)
        {
            _matchRepository = matchRepository;
            _petRepository = petRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _notificationService = notificationService;
            _messagePublisher = messagePublisher;
            _matchingLogic = matchingLogic;
        }


        public async Task<MatchDto> CreateMatchAsync(string initiatorPetId, CreateMatchDto createMatchDto)
        {
            var initiatorPet = await _petRepository.GetBySpecificationAsync(
                new PetWithDetailsSpecification(initiatorPetId))
                ?? throw new KeyNotFoundException("Initiator pet not found");

            var targetPet = await _petRepository.GetBySpecificationAsync(
                new PetWithDetailsSpecification(createMatchDto.TargetPetId))
                ?? throw new KeyNotFoundException("Target pet not found");

            var match = new Match
            {
                Id = Guid.NewGuid().ToString(),
                InitiatiorPetId = initiatorPetId,
                TargetPetId = createMatchDto.TargetPetId,
                InitiatiorPet = initiatorPet,
                TargetPet = targetPet,
                Purpose = createMatchDto.MatchPurpose,
                Status = MatchStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            await _matchRepository.AddAsync(match);
            var matchDto = _mapper.Map<MatchDto>(match);

            if (targetPet != null)
            {
                await _notificationService.NotifyNewMatch(targetPet.OwnerId, matchDto);
            }

            return matchDto;
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

            // Get existing matches for exclusion
            var existingMatches = await _matchRepository.FindBySpecificationAsync(
                new MatchWithDetailsSpecification(m =>
                    (m.InitiatiorPetId == petId || m.TargetPetId == petId) &&
                    (m.Status == MatchStatus.Pending || m.Status == MatchStatus.Accepted)));

            var matchedPetIds = existingMatches
                .SelectMany(m => new[] { m.InitiatiorPetId, m.TargetPetId })
                .Distinct()
                .ToList();

            // Get potential matches
            var potentialMatches = await _petRepository.FindBySpecificationAsync(
                new PetWithDetailsSpecification(p => !matchedPetIds.Contains(p.Id)));

            var matches = await _matchingLogic.FindPotentialMatches(
                initiatorPet,
                potentialMatches,
                initiatorOwner,
                purpose);

            // Send notifications
            foreach (var (match, distance) in matches)
            {
                var notification = _mapper.Map<PetMatchNotification>(match);
                notification.Distance = distance;

                await _messagePublisher.PublishAsync("pet.matches.found", notification);
            }

            return _mapper.Map<IEnumerable<PetDto>>(matches.Select(m => m.Pet));
        }

        public async Task<MatchDto> UpdateMatchStatusAsync(string matchId, MatchStatus status)
        {
            var match = await _matchRepository.GetBySpecificationAsync(
                new MatchWithDetailsSpecification(matchId))
                ?? throw new KeyNotFoundException("Match not found");


            match.Status = status;
            match.UpdatedAt = DateTime.UtcNow;

            await _matchRepository.UpdateAsync(match);
            var matchDto = _mapper.Map<MatchDto>(match);

            var initiatorPet = await _petRepository.GetBySpecificationAsync(
                new PetWithDetailsSpecification(match.InitiatiorPetId));
            var targetPet = await _petRepository.GetBySpecificationAsync(
                new PetWithDetailsSpecification(match.TargetPetId));

            if (initiatorPet != null)
            {
                await _notificationService.NotifyMatchStatusUpdate(initiatorPet.OwnerId, matchDto);
            }
            if (targetPet != null)
            {
                await _notificationService.NotifyMatchStatusUpdate(targetPet.OwnerId, matchDto);
            }

            return matchDto;
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
    }
}
