using AutoMapper;
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
            IMatchingLogicService matchingLogic)
        {
            _petRepository = petRepository;
            _matchRepository = matchRepository;
            _mapper = mapper;
            _environment = environment;
            _userManager = userManager;
            _messagePublisher = messagePublisher;
            _matchingLogic = matchingLogic;
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

            if (createPetDto.Photos != null && createPetDto.Photos.Any())
            {
                foreach (var photo in createPetDto.Photos)
                {
                    var fileName = await SaveFileAsync(photo, "pets/photos");
                    pet.Photos.Add(fileName);
                }
            }

            if (createPetDto.Videos != null && createPetDto.Videos.Any())
            {
                foreach (var video in createPetDto.Videos)
                {
                    var fileName = await SaveFileAsync(video, "pets/videos");
                    pet.Videos.Add(fileName);
                }
            }

            await _petRepository.AddAsync(pet);

            await NotifyPotentialMatches(pet);

            return _mapper.Map<PetDto>(pet);
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

            var matches = await _matchRepository.FindAsync(m =>
                m.InitiatiorPetId == id ||
                m.TargetPetId == id);

            foreach (var match in matches)
            {
                await _matchRepository.DeleteAsync(match);
            }

            // Delete all associated files
            foreach (var photo in pet.Photos)
            {
                var photoPath = Path.Combine(_environment.WebRootPath, "uploads", "pets/photos", photo);
                if (File.Exists(photoPath))
                    File.Delete(photoPath);
            }

            foreach (var video in pet.Videos)
            {
                var videoPath = Path.Combine(_environment.WebRootPath, "uploads", "pets/videos", video);
                if (File.Exists(videoPath))
                    File.Delete(videoPath);
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