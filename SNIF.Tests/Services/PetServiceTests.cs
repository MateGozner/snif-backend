using AutoMapper;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Moq;
using SNIF.Application.Services;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Core.Specifications;
using SNIF.Infrastructure.Repository;
using Match = SNIF.Core.Entities.Match;

namespace SNIF.Tests.Services;

public class PetServiceTests
{
    private readonly Mock<IRepository<Pet>> _petRepo = new();
    private readonly Mock<IRepository<Match>> _matchRepo = new();
    private readonly Mock<IRepository<PetMedia>> _mediaRepo = new();
    private readonly Mock<IRepository<DiscoveryPreferences>> _discoveryPrefsRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IWebHostEnvironment> _env = new();
    private readonly Mock<IMessagePublisher> _messagePublisher = new();
    private readonly Mock<IMatchingLogicService> _matchingLogic = new();
    private readonly Mock<IMediaStorageService> _mediaStorage = new();
    private readonly Mock<IEntitlementService> _entitlementService = new();

    private PetService CreateService() => new(
        _petRepo.Object,
        _matchRepo.Object,
        _mapper.Object,
        _env.Object,
        _userRepo.Object,
        _messagePublisher.Object,
        _matchingLogic.Object,
        _mediaRepo.Object,
        _discoveryPrefsRepo.Object,
        _mediaStorage.Object,
        _entitlementService.Object);

    private static EntitlementSnapshotDto CreateEntitlement(
        SubscriptionPlan plan = SubscriptionPlan.Free,
        int totalPets = 0,
        string? lockedPetId = null) => new()
    {
        BillingPlan = plan,
        EffectivePlan = plan,
        EffectiveStatus = EntitlementStatus.Active,
        SubscriptionStatus = SubscriptionStatus.Active,
        Limits = PlanLimits.GetLimits(plan),
        TotalPets = totalPets,
        ActivePets = lockedPetId == null ? totalPets : Math.Max(0, totalPets - 1),
        LockedPets = lockedPetId == null ? 0 : 1,
        IsOverPetLimit = lockedPetId != null,
        LockedPetIds = lockedPetId == null ? Array.Empty<string>() : new[] { lockedPetId },
        PetStates = lockedPetId == null
            ? Array.Empty<PetEntitlementStateDto>()
            : new[]
            {
                new PetEntitlementStateDto
                {
                    PetId = lockedPetId,
                    PetName = "Locked Pet",
                    CreatedAt = DateTime.UtcNow,
                    IsLocked = true,
                    LockReason = "Pet locked until subscription is upgraded."
                }
            }
    };

    private static Pet CreatePet(string id = "pet1", string ownerId = "owner1") => new()
    {
        Id = id,
        Name = "Buddy",
        Species = "Dog",
        Breed = "Labrador",
        Age = 3,
        Gender = Gender.Male,
        OwnerId = ownerId,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task CreatePetAsync_ValidData_ReturnsPetDto()
    {
        var userId = "user1";
        var createDto = new CreatePetDto
        {
            Name = "Buddy",
            Species = "Dog",
            Breed = "Labrador",
            Age = 3,
            Gender = Gender.Male
        };

        var user = new User { Id = userId, Name = "Test User", UserName = "testuser" };
        var pet = CreatePet();
        var petDto = new PetDto { Id = "pet1", Name = "Buddy", Species = "Dog", Breed = "Labrador", Age = 3, OwnerId = userId };

        _userRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _entitlementService.Setup(s => s.GetEntitlementAsync(userId)).ReturnsAsync(CreateEntitlement(totalPets: 0));
        _mapper.Setup(m => m.Map<Pet>(createDto)).Returns(pet);
        _petRepo.Setup(r => r.AddAsync(It.IsAny<Pet>())).ReturnsAsync(pet);
        _petRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>())).ReturnsAsync(pet);
        _petRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>())).ReturnsAsync(new List<Pet>());
        _mapper.Setup(m => m.Map<PetDto>(It.IsAny<Pet>())).Returns(petDto);
        _messagePublisher.Setup(p => p.PublishPetCreatedAsync(It.IsAny<Pet>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.CreatePetAsync(userId, createDto);

        result.Should().NotBeNull();
        result.Name.Should().Be("Buddy");
        _petRepo.Verify(r => r.AddAsync(It.IsAny<Pet>()), Times.Once);
    }

    [Fact]
    public async Task CreatePetAsync_UserNotFound_ThrowsKeyNotFound()
    {
        _userRepo.Setup(r => r.GetByIdAsync("missing")).ReturnsAsync((User?)null);
        _entitlementService.Setup(s => s.GetEntitlementAsync("missing")).ReturnsAsync(CreateEntitlement());

        var service = CreateService();

        await service.Invoking(s => s.CreatePetAsync("missing", new CreatePetDto
        {
            Name = "Buddy", Species = "Dog", Breed = "Lab", Age = 1
        })).Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetPetByIdAsync_ValidId_ReturnsPetDto()
    {
        var pet = CreatePet();
        var petDto = new PetDto { Id = "pet1", Name = "Buddy", Species = "Dog", Breed = "Labrador", Age = 3, OwnerId = "owner1" };

        _petRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>())).ReturnsAsync(pet);
        _mapper.Setup(m => m.Map<PetDto>(pet)).Returns(petDto);
        _entitlementService.Setup(s => s.GetEntitlementAsync("owner1")).ReturnsAsync(CreateEntitlement(totalPets: 1));

        var service = CreateService();
        var result = await service.GetPetByIdAsync("pet1");

        result.Should().NotBeNull();
        result.Id.Should().Be("pet1");
    }

    [Fact]
    public async Task GetPetByIdAsync_InvalidId_ThrowsKeyNotFound()
    {
        _petRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>())).ReturnsAsync((Pet?)null);
        _mapper.Setup(m => m.Map<PetDto>((Pet?)null)).Returns((PetDto?)null);

        var service = CreateService();

        await service.Invoking(s => s.GetPetByIdAsync("nonexistent"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetUserPetsAsync_ReturnsOnlyOwnerPets()
    {
        var pets = new List<Pet> { CreatePet("p1", "owner1"), CreatePet("p2", "owner1") };
        var petDtos = new List<PetDto>
        {
            new() { Id = "p1", Name = "Buddy", Species = "Dog", Breed = "Lab", OwnerId = "owner1" },
            new() { Id = "p2", Name = "Rex", Species = "Dog", Breed = "Lab", OwnerId = "owner1" }
        };

        _petRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>())).ReturnsAsync(pets);
        _mapper.Setup(m => m.Map<IEnumerable<PetDto>>(pets)).Returns(petDtos);
        _entitlementService.Setup(s => s.GetEntitlementAsync("owner1")).ReturnsAsync(CreateEntitlement(totalPets: 2));

        var service = CreateService();
        var result = await service.GetUserPetsAsync("owner1");

        result.Should().HaveCount(2);
        result.All(p => p.OwnerId == "owner1").Should().BeTrue();
    }

    [Fact]
    public async Task UpdatePetAsync_ValidId_UpdatesFields()
    {
        var pet = CreatePet();
        var updateDto = new UpdatePetDto { Name = "Updated" };
        var updatedPetDto = new PetDto { Id = "pet1", Name = "Updated", Species = "Dog", Breed = "Labrador", OwnerId = "owner1" };

        _petRepo.Setup(r => r.GetByIdAsync("pet1")).ReturnsAsync(pet);
        _petRepo.Setup(r => r.UpdateAsync(It.IsAny<Pet>())).Returns(Task.CompletedTask);
        _mapper.Setup(m => m.Map(updateDto, pet));
        _mapper.Setup(m => m.Map<PetDto>(It.IsAny<Pet>())).Returns(updatedPetDto);
        _entitlementService.Setup(s => s.GetEntitlementAsync("owner1")).ReturnsAsync(CreateEntitlement(totalPets: 1));

        var service = CreateService();
        var result = await service.UpdatePetAsync("pet1", updateDto);

        result.Name.Should().Be("Updated");
        _petRepo.Verify(r => r.UpdateAsync(It.IsAny<Pet>()), Times.Once);
    }

    [Fact]
    public async Task DeletePetAsync_RemovesPetAndMedia()
    {
        var pet = CreatePet();
        pet.Media = new List<PetMedia>
        {
            new() { Id = "m1", PetId = "pet1", FileName = "file1.jpg", ContentType = "image/jpeg", CreatedAt = DateTime.UtcNow }
        };

        _petRepo.Setup(r => r.GetByIdAsync("pet1")).ReturnsAsync(pet);
        _matchRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Match, bool>>>()))
            .ReturnsAsync(new List<Match>());
        _mediaStorage.Setup(s => s.DeleteAsync("file1.jpg")).ReturnsAsync(true);
        _petRepo.Setup(r => r.DeleteAsync(pet)).Returns(Task.CompletedTask);

        var service = CreateService();
        await service.DeletePetAsync("pet1");

        _mediaStorage.Verify(s => s.DeleteAsync("file1.jpg"), Times.Once);
        _petRepo.Verify(r => r.DeleteAsync(pet), Times.Once);
    }

    [Fact]
    public async Task DeletePetAsync_InvalidId_ThrowsKeyNotFound()
    {
        _petRepo.Setup(r => r.GetByIdAsync("bad")).ReturnsAsync((Pet?)null);

        var service = CreateService();

        await service.Invoking(s => s.DeletePetAsync("bad"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task AddPetMediaAsync_UploadsViaMediaStorage()
    {
        var pet = CreatePet();
        var base64 = Convert.ToBase64String(new byte[] { 1, 2, 3 });
        var mediaDto = new AddMediaDto
        {
            Base64Data = base64,
            ContentType = "image/jpeg",
            FileName = "photo.jpg",
            Type = MediaType.Photo
        };

        _petRepo.Setup(r => r.GetByIdAsync("pet1")).ReturnsAsync(pet);
        _mediaStorage.Setup(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg"))
            .ReturnsAsync("https://storage/photo.jpg");
        _mediaRepo.Setup(r => r.AddAsync(It.IsAny<PetMedia>())).ReturnsAsync(new PetMedia
        {
            Id = "m1", PetId = "pet1", FileName = "https://storage/photo.jpg",
            ContentType = "image/jpeg", CreatedAt = DateTime.UtcNow
        });
        _petRepo.Setup(r => r.UpdateAsync(It.IsAny<Pet>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.AddPetMediaAsync("pet1", mediaDto, "https://localhost");

        result.Should().NotBeNull();
        _mediaStorage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), "image/jpeg"), Times.Once);
    }

    [Fact]
    public async Task AddPetMediaAsync_VideoUpload_ThrowsArgumentException()
    {
        var mediaDto = new AddMediaDto
        {
            Base64Data = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            ContentType = "video/mp4",
            FileName = "clip.mp4",
            Type = MediaType.Video
        };

        var service = CreateService();

        await service.Invoking(s => s.AddPetMediaAsync("pet1", mediaDto, "https://localhost"))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*closed beta*");

        _petRepo.Verify(r => r.GetByIdAsync(It.IsAny<string>()), Times.Never);
        _mediaStorage.Verify(s => s.UploadAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task CreatePetAsync_WhenOverPlanLimit_ThrowsInvalidOperationException()
    {
        var userId = "user1";
        var user = new User { Id = userId, Name = "Test User", UserName = "testuser" };

        _userRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _entitlementService.Setup(s => s.GetEntitlementAsync(userId))
            .ReturnsAsync(CreateEntitlement(totalPets: 2, lockedPetId: "pet-locked"));

        var service = CreateService();

        await service.Invoking(s => s.CreatePetAsync(userId, new CreatePetDto
        {
            Name = "Buddy",
            Species = "Dog",
            Breed = "Labrador",
            Age = 3,
            Gender = Gender.Male
        })).Should().ThrowAsync<InvalidOperationException>();
    }
}
