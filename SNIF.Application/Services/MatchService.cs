using AutoMapper;
using SNIF.Core.Contracts;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Specifications;
using SNIF.Core.Utilities;
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
        private readonly IMatchPipeline _matchPipeline;
        private readonly IPushNotificationService _pushNotificationService;
        private readonly IEntitlementService _entitlementService;
        [Obsolete("Use IMatchPipeline instead. Kept for backward compatibility.")]
        private readonly IMatchingLogicService _matchingLogic;

        public MatchService(IRepository<Match> matchRepository, IRepository<Pet> petRepository, IRepository<User> userRepository, IMapper mapper, INotificationService notificationService, IMessagePublisher messagePublisher, IMatchingLogicService matchingLogic, IMatchPipeline matchPipeline, IPushNotificationService pushNotificationService, IEntitlementService entitlementService)
        {
            _matchRepository = matchRepository;
            _petRepository = petRepository;
            _userRepository = userRepository;
            _mapper = mapper;
            _notificationService = notificationService;
            _messagePublisher = messagePublisher;
            _matchingLogic = matchingLogic;
            _matchPipeline = matchPipeline;
            _pushNotificationService = pushNotificationService;
            _entitlementService = entitlementService;
        }


        public async Task<MatchDto> CreateMatchAsync(string userId, CreateMatchDto createMatchDto)
        {
            var initiatorPet = await _petRepository.GetBySpecificationAsync(
                new PetWithDetailsSpecification(createMatchDto.InitiatorPetId))
                ?? throw new KeyNotFoundException("Initiator pet not found");

            if (initiatorPet.OwnerId != userId)
                throw new UnauthorizedAccessException("You can only create matches from your own pet");

            await _entitlementService.EnsurePetCanUsePremiumActionsAsync(userId, initiatorPet.Id, "likes");

            var targetPet = await _petRepository.GetBySpecificationAsync(
                new PetWithDetailsSpecification(createMatchDto.TargetPetId))
                ?? throw new KeyNotFoundException("Target pet not found");

            var targetEntitlement = await _entitlementService.GetEntitlementAsync(targetPet.OwnerId);
            if (_entitlementService.IsPetLocked(targetEntitlement, targetPet.Id))
                throw new InvalidOperationException("The selected pet is currently unavailable under its owner's plan.");

            var match = new Match
            {
                Id = Guid.NewGuid().ToString(),
                InitiatiorPetId = createMatchDto.InitiatorPetId,
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
                await _pushNotificationService.SendPushAsync(
                    targetPet.OwnerId,
                    "New Match! \ud83d\udc3e",
                    $"{initiatorPet.Name} wants to match with {targetPet.Name}",
                    new Dictionary<string, string> { ["type"] = "match", ["matchId"] = match.Id });
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

        public async Task<string> GetPeerUserIdAsync(string matchId, string userId)
        {
            var match = await GetMatchByIdAsync(matchId);

            if (match.InitiatorPet.OwnerId == userId)
            {
                return match.TargetPet.OwnerId;
            }

            if (match.TargetPet.OwnerId == userId)
            {
                return match.InitiatorPet.OwnerId;
            }

            throw new UnauthorizedAccessException("User not authorized for this match");
        }

        public async Task<IEnumerable<MatchDto>> GetPetMatchesAsync(string petId)
        {
            var matches = await _matchRepository.FindBySpecificationAsync(
                new MatchWithDetailsSpecification(m =>
                    (m.InitiatiorPetId == petId ||
                    m.TargetPetId == petId) &&
                    m.Status == MatchStatus.Accepted));

            return _mapper.Map<IEnumerable<MatchDto>>(matches);
        }

        public async Task<IEnumerable<PetDto>> GetPotentialMatchesAsync(string userId, string petId, PetPurpose? purpose)
        {
            var initiatorPet = await _petRepository.GetBySpecificationAsync(
                new PetWithDetailsSpecification(petId))
                ?? throw new KeyNotFoundException("Pet not found");

            if (initiatorPet.OwnerId != userId)
                throw new UnauthorizedAccessException("You can only view matches for your own pet");

            await _entitlementService.EnsurePetCanUsePremiumActionsAsync(userId, initiatorPet.Id, "discovery");

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
                .ToHashSet();

            // Get all pets for the pipeline (pipeline handles filtering internally)
            var allPets = await _petRepository.FindBySpecificationAsync(
                new PetWithDetailsSpecification(p => p.Id != petId));

            var rankedCandidates = await _matchPipeline.ExecuteAsync(
                initiatorPet,
                allPets,
                initiatorOwner,
                matchedPetIds,
                purpose);

            return _mapper.Map<IEnumerable<PetDto>>(rankedCandidates.Select(c => c.Pet));
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

            // Send push notification for accepted matches
            if (status == MatchStatus.Accepted && initiatorPet != null && targetPet != null)
            {
                await _pushNotificationService.SendPushAsync(
                    initiatorPet.OwnerId,
                    "Match Accepted! \ud83c\udf89",
                    $"{initiatorPet.Name} matched with {targetPet.Name}",
                    new Dictionary<string, string> { ["type"] = "match", ["matchId"] = match.Id });
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

        public async Task<IDictionary<string, IEnumerable<MatchDto>>> GetBulkMatchesAsync(IEnumerable<string> petIds,
    MatchStatus? status = null)
        {
            var results = new Dictionary<string, IEnumerable<MatchDto>>();

            foreach (var petId in petIds)
            {
                var matches = status switch
                {
                    MatchStatus.Pending => await GetPendingMatchesForPetAsync(petId),
                    null => await GetPetMatchesAsync(petId),
                    _ => throw new ArgumentException("Invalid status")
                };
                results[petId] = matches;
            }

            return results;
        }

        public async Task<List<WhoLikedYouDto>> GetWhoLikedYouAsync(string userId, SubscriptionPlan plan)
        {
            // Find all pets owned by this user
            var userPets = await _petRepository.FindBySpecificationAsync(
                new PetWithDetailsSpecification(p => p.OwnerId == userId));

            var userPetIds = userPets.Select(p => p.Id).ToHashSet();

            // Find pending matches where the user's pets are the target (i.e., someone liked them)
            var pendingMatches = await _matchRepository.FindBySpecificationAsync(
                new MatchWithDetailsSpecification(m =>
                    userPetIds.Contains(m.TargetPetId) &&
                    m.Status == MatchStatus.Pending));

            var isBlurred = plan == SubscriptionPlan.Free;

            return pendingMatches
                .OrderByDescending(m => m.CreatedAt)
                .Select(m =>
                {
                    var initiatorPet = m.InitiatiorPet;
                    var firstMedia = initiatorPet?.Media?.FirstOrDefault(media => media.Type == MediaType.Photo)
                        ?? initiatorPet?.Media?.FirstOrDefault();
                    string? photoUrl = null;
                    if (firstMedia != null)
                    {
                        photoUrl = MediaPathResolver.ResolvePetMediaPath(firstMedia.FileName, firstMedia.Type);
                    }

                    return new WhoLikedYouDto
                    {
                        MatchId = m.Id,
                        PetName = isBlurred
                            ? (initiatorPet?.Name?.Length > 0 ? initiatorPet.Name[0] + "***" : "***")
                            : (initiatorPet?.Name ?? "Unknown"),
                        PetPhotoUrl = isBlurred ? null : photoUrl,
                        Breed = isBlurred ? null : initiatorPet?.Breed,
                        LikedAt = m.CreatedAt,
                        IsBlurred = isBlurred,
                    };
                })
                .ToList();
        }

    }
}
