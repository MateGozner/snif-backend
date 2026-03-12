using AutoMapper;
using Azure.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SNIF.Core.Contracts;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Core.Specifications;
using SNIF.Core.Utilities;
using SNIF.Infrastructure.Repository;

namespace SNIF.Application.Services
{
    public class PetService : IPetService
    {
        private readonly IRepository<Pet> _petRepository;
        private readonly IRepository<Match> _matchRepository;
        private readonly IRepository<PetMedia> _mediaRepository;
        private readonly IRepository<DiscoveryPreferences> _discoveryPrefsRepository;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _environment;
        private readonly IRepository<User> _userManager;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IMatchingLogicService _matchingLogic;
        private readonly IMediaStorageService _mediaStorage;
        private readonly IEntitlementService _entitlementService;

        public PetService(
            IRepository<Pet> petRepository,
            IRepository<Match> matchRepository,
            IMapper mapper,
            IWebHostEnvironment environment,
            IRepository<User> userManager,
            IMessagePublisher messagePublisher,
            IMatchingLogicService matchingLogic,
            IRepository<PetMedia> mediaRepository,
            IRepository<DiscoveryPreferences> discoveryPrefsRepository,
            IMediaStorageService mediaStorage,
            IEntitlementService entitlementService)
        {
            _petRepository = petRepository;
            _matchRepository = matchRepository;
            _mapper = mapper;
            _environment = environment;
            _userManager = userManager;
            _messagePublisher = messagePublisher;
            _matchingLogic = matchingLogic;
            _mediaRepository = mediaRepository;
            _discoveryPrefsRepository = discoveryPrefsRepository;
            _mediaStorage = mediaStorage;
            _entitlementService = entitlementService;
        }

        public async Task<IEnumerable<PetDto>> GetUserPetsAsync(string userId)
        {
            var spec = new PetWithDetailsSpecification(p => p.OwnerId == userId);
            var pets = await _petRepository.FindBySpecificationAsync(spec);
            var entitlement = await _entitlementService.GetEntitlementAsync(userId);
            return ApplyEntitlement(_mapper.Map<IEnumerable<PetDto>>(pets), entitlement);
        }

        public async Task<PetDto> GetPetByIdAsync(string id)
        {
            var spec = new PetWithDetailsSpecification(id);
            var pet = await _petRepository.GetBySpecificationAsync(spec);
            if (pet == null)
                throw new KeyNotFoundException($"Pet with ID {id} not found");

            var entitlement = await _entitlementService.GetEntitlementAsync(pet.OwnerId);
            return ApplyEntitlement(_mapper.Map<PetDto>(pet), entitlement)
                ?? throw new KeyNotFoundException($"Pet with ID {id} not found");
        }

        public async Task<PetDto> CreatePetAsync(string userId, CreatePetDto createPetDto)
        {
            var user = await _userManager.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException($"User with ID {userId} not found");
            var entitlement = await _entitlementService.GetEntitlementAsync(userId);

            if (entitlement.IsOverPetLimit || entitlement.TotalPets >= entitlement.Limits.MaxPets)
                throw new InvalidOperationException("Pet limit reached for the current plan. Upgrade to add more pets.");

            var pet = _mapper.Map<Pet>(createPetDto);
            pet.OwnerId = userId;
            pet.CreatedAt = DateTime.UtcNow;

            await _petRepository.AddAsync(pet);

            // Handle media if provided
            if (createPetDto.Media != null)
            {
                foreach (var mediaDto in createPetDto.Media)
                {
                    EnsureMediaSupportedForBeta(mediaDto.Type);

                    var mediaId = Guid.NewGuid().ToString();
                    var extension = Path.GetExtension(mediaDto.FileName);
                    var fileName = $"{mediaId}{extension}";

                    var fileBytes = Convert.FromBase64String(mediaDto.Base64Data);
                    using var stream = new MemoryStream(fileBytes);
                    var storedUrl = await _mediaStorage.UploadAsync(stream, fileName, mediaDto.ContentType);

                    var media = new PetMedia
                    {
                        Id = mediaId,
                        PetId = pet.Id,
                        FileName = storedUrl,
                        ContentType = mediaDto.ContentType,
                        Size = fileBytes.Length,
                        Type = mediaDto.Type,
                        Title = mediaDto.Title ?? string.Empty,
                        Description = mediaDto.Description ?? string.Empty,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _mediaRepository.AddAsync(media);
                }
            }

            await NotifyPotentialMatches(pet);

            var updatedPet = await _petRepository.GetBySpecificationAsync(new PetWithDetailsSpecification(pet.Id));
            var refreshedEntitlement = await _entitlementService.GetEntitlementAsync(userId);
            return ApplyEntitlement(_mapper.Map<PetDto>(updatedPet), refreshedEntitlement)!;
        }


        public async Task<MediaResponseDto> AddPetMediaAsync(string petId, AddMediaDto mediaDto, string baseUrl)
        {
            EnsureMediaSupportedForBeta(mediaDto.Type);

            var pet = await _petRepository.GetByIdAsync(petId)
                ?? throw new KeyNotFoundException($"Pet with ID {petId} not found");

            // Convert base64 to bytes
            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(mediaDto.Base64Data);
            }
            catch (FormatException)
            {
                throw new ArgumentException("Invalid base64 string");
            }

            var mediaId = Guid.NewGuid().ToString();
            var extension = Path.GetExtension(mediaDto.FileName);
            var fileName = $"{mediaId}{extension}";

            using var stream = new MemoryStream(fileBytes);
            var storedUrl = await _mediaStorage.UploadAsync(stream, fileName, mediaDto.ContentType);

            var media = new PetMedia
            {
                Id = mediaId,
                PetId = petId,
                FileName = storedUrl,
                ContentType = mediaDto.ContentType,
                Size = fileBytes.Length,
                Type = mediaDto.Type,
                Title = mediaDto.Title ?? string.Empty,
                Description = mediaDto.Description ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            await _mediaRepository.AddAsync(media);

            pet.UpdatedAt = DateTime.UtcNow;
            await _petRepository.UpdateAsync(pet);

            return CreateMediaResponse(media, baseUrl);
        }

        public async Task<IEnumerable<MediaResponseDto>> GetPetMediaAsync(
            string petId,
            MediaType? type = null,
            string baseUrl = "")
        {
            var pet = await _petRepository.GetByIdAsync(petId)
                ?? throw new KeyNotFoundException($"Pet with ID {petId} not found");

            // Get media using repository
            var media = await _mediaRepository.FindAsync(m => m.PetId == petId);

            // Apply type filter if specified
            if (type.HasValue)
            {
                media = media.Where(m => m.Type == type.Value).ToList();
            }

            return media.Select(m => CreateMediaResponse(m, baseUrl));
        }


        public async Task<MediaResponseDto> GetMediaByIdAsync(string mediaId, string baseUrl)
        {
            var specification = new PetWithMediaSpecification(mediaId);
            var pet = await _petRepository.GetBySpecificationAsync(specification);

            var media = pet?.Media.FirstOrDefault(m => m.Id == mediaId)
                ?? throw new KeyNotFoundException($"Media with ID {mediaId} not found");

            return CreateMediaResponse(media, baseUrl);
        }


        public async Task DeletePetMediaAsync(string petId, string mediaId)
        {
            var pet = await _petRepository.GetByIdAsync(petId)
                ?? throw new KeyNotFoundException($"Pet with ID {petId} not found");

            var media = await _mediaRepository.GetByIdAsync(mediaId)
                ?? throw new KeyNotFoundException($"Media with ID {mediaId} not found");

            await _mediaStorage.DeleteAsync(media.FileName);

            await _mediaRepository.DeleteAsync(media);
            pet.UpdatedAt = DateTime.UtcNow;
            await _petRepository.UpdateAsync(pet);
        }

        private MediaResponseDto CreateMediaResponse(PetMedia media, string baseUrl)
        {
            return new MediaResponseDto
            {
                Id = media.Id,
                Url = MediaPathResolver.ResolvePetMediaPath(media.FileName, media.Type) ?? string.Empty,
                Type = media.Type,
                FileName = media.FileName,
                ContentType = media.ContentType,
                Size = media.Size,
                CreatedAt = media.CreatedAt,
                UpdatedAt = media.UpdatedAt,
                Links = new Dictionary<string, string>
                {
                    ["self"] = $"/api/pets/{media.PetId}/media/{media.Id}",
                    ["pet"] = $"/api/pets/{media.PetId}"
                }
            };
        }

        private static void EnsureMediaSupportedForBeta(MediaType mediaType)
        {
            if (mediaType == MediaType.Video)
                throw new ArgumentException("Pet video uploads are disabled for the closed beta. Upload photos instead.");
        }

        private async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            using var stream = file.OpenReadStream();
            return await _mediaStorage.UploadAsync(stream, fileName, file.ContentType);
        }

        public async Task<string> AddPetPhotoAsync(string id, IFormFile photo)
        {
            var pet = await _petRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Pet with ID {id} not found");

            var fileName = await SaveFileAsync(photo, "pets/photos");
            pet.Photos.Add(fileName);
            pet.UpdatedAt = DateTime.UtcNow;

            await _petRepository.UpdateAsync(pet);
            return fileName;
        }

        public async Task<string> AddPetVideoAsync(string id, IFormFile video)
        {
            var pet = await _petRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Pet with ID {id} not found");

            var fileName = await SaveFileAsync(video, "pets/videos");
            pet.Videos.Add(fileName);
            pet.UpdatedAt = DateTime.UtcNow;

            await _petRepository.UpdateAsync(pet);
            return fileName;
        }

        public async Task DeletePetPhotoAsync(string id, string photoName)
        {
            var pet = await _petRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Pet with ID {id} not found");

            if (!pet.Photos.Remove(photoName))
                throw new KeyNotFoundException($"Photo {photoName} not found");

            await _mediaStorage.DeleteAsync(photoName);

            pet.UpdatedAt = DateTime.UtcNow;
            await _petRepository.UpdateAsync(pet);
        }

        public async Task DeletePetVideoAsync(string id, string videoName)
        {
            var pet = await _petRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Pet with ID {id} not found");

            if (!pet.Videos.Remove(videoName))
                throw new KeyNotFoundException($"Video {videoName} not found");

            await _mediaStorage.DeleteAsync(videoName);

            pet.UpdatedAt = DateTime.UtcNow;
            await _petRepository.UpdateAsync(pet);
        }

        public async Task DeletePetAsync(string id)
        {
            var pet = await _petRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Pet with ID {id} not found");

            // Delete all media files
            foreach (var media in pet.Media)
            {
                await _mediaStorage.DeleteAsync(media.FileName);
            }

            // Delete matches
            var matches = await _matchRepository.FindAsync(m =>
                m.InitiatiorPetId == id || m.TargetPetId == id);

            foreach (var match in matches)
            {
                await _matchRepository.DeleteAsync(match);
            }

            await _petRepository.DeleteAsync(pet);
        }

        public async Task<PetDto> UpdatePetAsync(string id, UpdatePetDto updatePetDto)
        {
            var pet = await _petRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Pet with ID {id} not found");

            _mapper.Map(updatePetDto, pet);
            pet.UpdatedAt = DateTime.UtcNow;

            await _petRepository.UpdateAsync(pet);
            var entitlement = await _entitlementService.GetEntitlementAsync(pet.OwnerId);
            return ApplyEntitlement(_mapper.Map<PetDto>(pet), entitlement)!;
        }

        private IEnumerable<PetDto> ApplyEntitlement(IEnumerable<PetDto> pets, EntitlementSnapshotDto entitlement)
        {
            return pets.Select(pet => ApplyEntitlement(pet, entitlement)!).ToArray();
        }

        private PetDto? ApplyEntitlement(PetDto? pet, EntitlementSnapshotDto entitlement)
        {
            if (pet == null)
                return null;

            var petState = entitlement.PetStates.FirstOrDefault(state => state.PetId == pet.Id);
            return pet with
            {
                IsLocked = petState?.IsLocked ?? false,
                EntitlementLockReason = petState?.LockReason
            };
        }

        private async Task NotifyPotentialMatches(Pet newPet)
        {
            await _messagePublisher.PublishPetCreatedAsync(newPet);

            var potentialMatches = await _petRepository.FindBySpecificationAsync(
                new PetWithDetailsSpecification(p => p.Id != newPet.Id));

            foreach (var existingPet in potentialMatches)
            {
                var existingPetOwner = await _userManager.GetBySpecificationAsync(
                    new UserWithDetailsSpecification(existingPet.OwnerId));

                if (existingPetOwner?.Preferences == null)
                    continue;

                var matches = await _matchingLogic.FindPotentialMatches(
                    existingPet,
                    new[] { newPet },
                    existingPetOwner);

                foreach (var (_, distance) in matches)
                {
                    var notification = _mapper.Map<PetMatchNotification>(newPet);
                    notification.Distance = distance;
                    notification.NotifiedAt = DateTime.UtcNow;

                    await _messagePublisher.PublishMatchNotificationAsync(
                        existingPetOwner.Id,
                        notification);
                }
            }
        }

        public async Task<DiscoveryPreferencesDto> GetDiscoveryPreferencesAsync(string petId)
        {
            _ = await _petRepository.GetByIdAsync(petId)
                ?? throw new KeyNotFoundException($"Pet with ID {petId} not found");

            var existing = await _discoveryPrefsRepository.FindAsync(d => d.PetId == petId);
            var prefs = existing.FirstOrDefault();

            if (prefs == null)
            {
                return new DiscoveryPreferencesDto
                {
                    PetId = petId,
                    AllowOtherBreeds = true,
                    AllowOtherSpecies = false,
                };
            }

            return new DiscoveryPreferencesDto
            {
                Id = prefs.Id,
                PetId = prefs.PetId,
                AllowOtherBreeds = prefs.AllowOtherBreeds,
                AllowOtherSpecies = prefs.AllowOtherSpecies,
                MinAge = prefs.MinAge,
                MaxAge = prefs.MaxAge,
                PreferredGender = prefs.PreferredGender,
                PreferredPurposes = prefs.PreferredPurposes?
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .ToList(),
            };
        }

        public async Task<DiscoveryPreferencesDto> UpdateDiscoveryPreferencesAsync(string petId, UpdateDiscoveryPreferencesDto dto)
        {
            _ = await _petRepository.GetByIdAsync(petId)
                ?? throw new KeyNotFoundException($"Pet with ID {petId} not found");

            var existing = await _discoveryPrefsRepository.FindAsync(d => d.PetId == petId);
            var prefs = existing.FirstOrDefault();

            if (prefs == null)
            {
                prefs = new DiscoveryPreferences
                {
                    PetId = petId,
                    AllowOtherBreeds = dto.AllowOtherBreeds,
                    AllowOtherSpecies = dto.AllowOtherSpecies,
                    MinAge = dto.MinAge,
                    MaxAge = dto.MaxAge,
                    PreferredGender = dto.PreferredGender,
                    PreferredPurposes = dto.PreferredPurposes != null
                        ? string.Join(",", dto.PreferredPurposes)
                        : null,
                    CreatedAt = DateTime.UtcNow,
                };
                await _discoveryPrefsRepository.AddAsync(prefs);
            }
            else
            {
                prefs.AllowOtherBreeds = dto.AllowOtherBreeds;
                prefs.AllowOtherSpecies = dto.AllowOtherSpecies;
                prefs.MinAge = dto.MinAge;
                prefs.MaxAge = dto.MaxAge;
                prefs.PreferredGender = dto.PreferredGender;
                prefs.PreferredPurposes = dto.PreferredPurposes != null
                    ? string.Join(",", dto.PreferredPurposes)
                    : null;
                prefs.UpdatedAt = DateTime.UtcNow;
                await _discoveryPrefsRepository.UpdateAsync(prefs);
            }

            return new DiscoveryPreferencesDto
            {
                Id = prefs.Id,
                PetId = prefs.PetId,
                AllowOtherBreeds = prefs.AllowOtherBreeds,
                AllowOtherSpecies = prefs.AllowOtherSpecies,
                MinAge = prefs.MinAge,
                MaxAge = prefs.MaxAge,
                PreferredGender = prefs.PreferredGender,
                PreferredPurposes = dto.PreferredPurposes,
            };
        }
    }
}