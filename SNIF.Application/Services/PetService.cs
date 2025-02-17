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
using SNIF.Core.Specifications;
using SNIF.Infrastructure.Repository;

namespace SNIF.Application.Services
{
    public class PetService : IPetService
    {
        private readonly IRepository<Pet> _petRepository;
        private readonly IRepository<Match> _matchRepository;
        private readonly IRepository<PetMedia> _mediaRepository;
        private readonly IMapper _mapper;
        private readonly IWebHostEnvironment _environment;
        private readonly IRepository<User> _userManager;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IMatchingLogicService _matchingLogic;

        public PetService(
            IRepository<Pet> petRepository,
            IRepository<Match> matchRepository,
            IMapper mapper,
            IWebHostEnvironment environment,
            IRepository<User> userManager,
            IMessagePublisher messagePublisher,
            IMatchingLogicService matchingLogic,
            IRepository<PetMedia> mediaRepository)
        {
            _petRepository = petRepository;
            _matchRepository = matchRepository;
            _mapper = mapper;
            _environment = environment;
            _userManager = userManager;
            _messagePublisher = messagePublisher;
            _matchingLogic = matchingLogic;
            _mediaRepository = mediaRepository;
        }

        public async Task<IEnumerable<PetDto>> GetUserPetsAsync(string userId)
        {
            var spec = new PetWithDetailsSpecification(p => p.OwnerId == userId);
            var pets = await _petRepository.FindBySpecificationAsync(spec);
            return _mapper.Map<IEnumerable<PetDto>>(pets);
        }

        public async Task<PetDto> GetPetByIdAsync(string id)
        {
            var spec = new PetWithDetailsSpecification(id);
            var pet = await _petRepository.GetBySpecificationAsync(spec);
            return _mapper.Map<PetDto>(pet) ?? throw new KeyNotFoundException($"Pet with ID {id} not found");
        }

        public async Task<PetDto> CreatePetAsync(string userId, CreatePetDto createPetDto)
        {
            var user = await _userManager.GetByIdAsync(userId)
                ?? throw new KeyNotFoundException($"User with ID {userId} not found");

            var pet = _mapper.Map<Pet>(createPetDto);
            pet.OwnerId = userId;
            pet.CreatedAt = DateTime.UtcNow;

            await _petRepository.AddAsync(pet);

            // Handle media if provided
            if (createPetDto.Media != null)
            {
                foreach (var mediaDto in createPetDto.Media)
                {
                    var mediaId = Guid.NewGuid().ToString();
                    var extension = Path.GetExtension(mediaDto.FileName);
                    var fileName = $"{mediaId}{extension}";

                    var fileBytes = Convert.FromBase64String(mediaDto.Base64Data);
                    var subFolder = mediaDto.Type == MediaType.Photo ? "photos" : "videos";
                    var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "pets", subFolder);
                    Directory.CreateDirectory(uploadPath);
                    var filePath = Path.Combine(uploadPath, fileName);

                    await File.WriteAllBytesAsync(filePath, fileBytes);

                    var media = new PetMedia
                    {
                        Id = mediaId,
                        PetId = pet.Id,
                        FileName = fileName,
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
            return _mapper.Map<PetDto>(updatedPet);
        }


        public async Task<MediaResponseDto> AddPetMediaAsync(string petId, AddMediaDto mediaDto, string baseUrl)
        {
            var pet = await _petRepository.GetByIdAsync(petId)
                ?? throw new KeyNotFoundException($"Pet with ID {petId} not found");

            var mediaId = Guid.NewGuid().ToString();
            var extension = Path.GetExtension(mediaDto.FileName);
            var fileName = $"{mediaId}{extension}";

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

            var subFolder = mediaDto.Type == MediaType.Photo ? "photos" : "videos";
            var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "pets", subFolder);
            Directory.CreateDirectory(uploadPath);
            var filePath = Path.Combine(uploadPath, fileName);

            await File.WriteAllBytesAsync(filePath, fileBytes);

            var media = new PetMedia
            {
                Id = mediaId,
                PetId = petId,
                FileName = fileName,
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

            var subFolder = media.Type == MediaType.Photo ? "photos" : "videos";
            var filePath = Path.Combine(_environment.WebRootPath, "uploads", "pets", subFolder, media.FileName);

            if (File.Exists(filePath))
                File.Delete(filePath);

            await _mediaRepository.DeleteAsync(media);
            pet.UpdatedAt = DateTime.UtcNow;
            await _petRepository.UpdateAsync(pet);
        }

        private MediaResponseDto CreateMediaResponse(PetMedia media, string baseUrl)
        {
            return new MediaResponseDto
            {
                Id = media.Id,
                Url = $"{baseUrl}/uploads/pets/{(media.Type == MediaType.Photo ? "photos" : "videos")}/{media.FileName}",
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

        private async Task<string> SaveFileAsync(IFormFile file, string folder)
        {
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", folder);
            Directory.CreateDirectory(uploadPath);
            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return fileName;
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

            var filePath = Path.Combine(_environment.WebRootPath, "uploads", "pets/photos", photoName);
            if (File.Exists(filePath))
                File.Delete(filePath);

            pet.UpdatedAt = DateTime.UtcNow;
            await _petRepository.UpdateAsync(pet);
        }

        public async Task DeletePetVideoAsync(string id, string videoName)
        {
            var pet = await _petRepository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Pet with ID {id} not found");

            if (!pet.Videos.Remove(videoName))
                throw new KeyNotFoundException($"Video {videoName} not found");

            var filePath = Path.Combine(_environment.WebRootPath, "uploads", "pets/videos", videoName);
            if (File.Exists(filePath))
                File.Delete(filePath);

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
                var subFolder = media.Type == MediaType.Photo ? "photos" : "videos";
                var filePath = Path.Combine(_environment.WebRootPath, "uploads", "pets", subFolder, media.FileName);

                if (File.Exists(filePath))
                    File.Delete(filePath);
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
            return _mapper.Map<PetDto>(pet);
        }

        private async Task NotifyPotentialMatches(Pet newPet)
        {
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

                    await _messagePublisher.PublishAsync("pet.matches.found", notification);
                }
            }
        }
    }
}