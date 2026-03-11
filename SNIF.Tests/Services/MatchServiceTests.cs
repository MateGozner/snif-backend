using AutoMapper;
using FluentAssertions;
using Moq;
using SNIF.Busniess.Services;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models;
using SNIF.Core.Specifications;
using SNIF.Infrastructure.Repository;
using Match = SNIF.Core.Entities.Match;

namespace SNIF.Tests.Services;

public class MatchServiceTests
{
    private readonly Mock<IRepository<Match>> _matchRepo = new();
    private readonly Mock<IRepository<Pet>> _petRepo = new();
    private readonly Mock<IRepository<User>> _userRepo = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IMessagePublisher> _messagePublisher = new();
    private readonly Mock<IMatchingLogicService> _matchingLogic = new();
    private readonly Mock<IMatchPipeline> _matchPipeline = new();
    private readonly Mock<IPushNotificationService> _pushNotification = new();
    private readonly Mock<IEntitlementService> _entitlementService = new();

    private MatchService CreateService() => new(
        _matchRepo.Object,
        _petRepo.Object,
        _userRepo.Object,
        _mapper.Object,
        _notificationService.Object,
        _messagePublisher.Object,
        _matchingLogic.Object,
        _matchPipeline.Object,
        _pushNotification.Object,
        _entitlementService.Object);

    private static EntitlementSnapshotDto CreateEntitlement(string? lockedPetId = null) => new()
    {
        BillingPlan = SubscriptionPlan.GoodBoy,
        EffectivePlan = SubscriptionPlan.GoodBoy,
        EffectiveStatus = EntitlementStatus.Active,
        SubscriptionStatus = SubscriptionStatus.Active,
        Limits = PlanLimits.GetLimits(SubscriptionPlan.GoodBoy),
        TotalPets = 1,
        ActivePets = lockedPetId == null ? 1 : 0,
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
                    PetName = "Locked",
                    CreatedAt = DateTime.UtcNow,
                    IsLocked = true,
                    LockReason = "Locked by entitlement"
                }
            }
    };

    private static Pet CreatePet(string id, string ownerId) => new()
    {
        Id = id, Name = $"Pet_{id}", Species = "Dog", Breed = "Lab",
        Age = 2, Gender = Gender.Male, OwnerId = ownerId, CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task CreateMatchAsync_ValidPets_ReturnsMatchDto()
    {
        var initiator = CreatePet("pet1", "owner1");
        var target = CreatePet("pet2", "owner2");

        _petRepo.Setup(r => r.GetBySpecificationAsync(
            It.Is<IQuerySpecification<Pet>>(s => true)))
            .ReturnsAsync((IQuerySpecification<Pet> spec) =>
            {
                // Return different pets based on call order
                return initiator;
            });

        // Setup to return initiator first, then target
        var callCount = 0;
        _petRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? initiator : target;
            });

        _matchRepo.Setup(r => r.AddAsync(It.IsAny<Match>())).ReturnsAsync((Match m) => m);

        var matchDto = new MatchDto
        {
            Id = "match1",
            InitiatorPet = new PetDto { Id = "pet1", Name = "Pet_pet1", Species = "Dog", Breed = "Lab", OwnerId = "owner1" },
            TargetPet = new PetDto { Id = "pet2", Name = "Pet_pet2", Species = "Dog", Breed = "Lab", OwnerId = "owner2" },
            Status = MatchStatus.Pending
        };
        _mapper.Setup(m => m.Map<MatchDto>(It.IsAny<Match>())).Returns(matchDto);
        _notificationService.Setup(n => n.NotifyNewMatch(It.IsAny<string>(), It.IsAny<MatchDto>())).Returns(Task.CompletedTask);
        _pushNotification.Setup(p => p.SendPushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>())).Returns(Task.CompletedTask);
        _entitlementService.Setup(s => s.EnsurePetCanUsePremiumActionsAsync("owner1", "pet1", "likes")).Returns(Task.CompletedTask);
        _entitlementService.Setup(s => s.GetEntitlementAsync("owner2")).ReturnsAsync(CreateEntitlement());

        var service = CreateService();
        var createDto = new CreateMatchDto
        {
            InitiatorPetId = "pet1",
            TargetPetId = "pet2",
            MatchPurpose = PetPurpose.Friendship
        };

        var result = await service.CreateMatchAsync("owner1", createDto);

        result.Should().NotBeNull();
        result.Status.Should().Be(MatchStatus.Pending);
        _matchRepo.Verify(r => r.AddAsync(It.IsAny<Match>()), Times.Once);
    }

    [Fact]
    public async Task CreateMatchAsync_PetNotFound_ThrowsKeyNotFound()
    {
        _petRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>()))
            .ReturnsAsync((Pet?)null);

        var service = CreateService();
        var createDto = new CreateMatchDto
        {
            InitiatorPetId = "missing",
            TargetPetId = "pet2",
            MatchPurpose = PetPurpose.Friendship
        };

        await service.Invoking(s => s.CreateMatchAsync("owner1", createDto))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CreateMatchAsync_LockedTargetPet_ThrowsInvalidOperationException()
    {
        var initiator = CreatePet("pet1", "owner1");
        var target = CreatePet("pet2", "owner2");

        var callCount = 0;
        _petRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? initiator : target;
            });

        _entitlementService.Setup(s => s.EnsurePetCanUsePremiumActionsAsync("owner1", "pet1", "likes")).Returns(Task.CompletedTask);
        _entitlementService.Setup(s => s.GetEntitlementAsync("owner2")).ReturnsAsync(CreateEntitlement("pet2"));
        _entitlementService.Setup(s => s.IsPetLocked(It.IsAny<EntitlementSnapshotDto>(), "pet2")).Returns(true);

        var service = CreateService();

        await service.Invoking(s => s.CreateMatchAsync("owner1", new CreateMatchDto
        {
            InitiatorPetId = "pet1",
            TargetPetId = "pet2",
            MatchPurpose = PetPurpose.Friendship
        })).Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateMatchStatusAsync_AcceptMatch_ChangesStatus()
    {
        var match = new Match
        {
            Id = "match1",
            InitiatiorPetId = "pet1",
            TargetPetId = "pet2",
            Status = MatchStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var initiator = CreatePet("pet1", "owner1");
        var target = CreatePet("pet2", "owner2");

        _matchRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>())).ReturnsAsync(match);
        _matchRepo.Setup(r => r.UpdateAsync(It.IsAny<Match>())).Returns(Task.CompletedTask);

        var callCount2 = 0;
        _petRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>()))
            .ReturnsAsync(() => { callCount2++; return callCount2 == 1 ? initiator : target; });

        var matchDto = new MatchDto
        {
            Id = "match1",
            Status = MatchStatus.Accepted,
            InitiatorPet = new PetDto { Id = "pet1", Name = "Pet_pet1", Species = "Dog", Breed = "Lab", OwnerId = "owner1" },
            TargetPet = new PetDto { Id = "pet2", Name = "Pet_pet2", Species = "Dog", Breed = "Lab", OwnerId = "owner2" }
        };
        _mapper.Setup(m => m.Map<MatchDto>(It.IsAny<Match>())).Returns(matchDto);
        _notificationService.Setup(n => n.NotifyMatchStatusUpdate(It.IsAny<string>(), It.IsAny<MatchDto>())).Returns(Task.CompletedTask);
        _pushNotification.Setup(p => p.SendPushAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Dictionary<string, string>>())).Returns(Task.CompletedTask);

        var service = CreateService();
        var result = await service.UpdateMatchStatusAsync("match1", MatchStatus.Accepted);

        result.Status.Should().Be(MatchStatus.Accepted);
        _matchRepo.Verify(r => r.UpdateAsync(It.Is<Match>(m => m.Status == MatchStatus.Accepted)), Times.Once);
    }

    [Fact]
    public async Task UpdateMatchStatusAsync_RejectMatch_ChangesStatus()
    {
        var match = new Match
        {
            Id = "match1",
            InitiatiorPetId = "pet1",
            TargetPetId = "pet2",
            Status = MatchStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _matchRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>())).ReturnsAsync(match);
        _matchRepo.Setup(r => r.UpdateAsync(It.IsAny<Match>())).Returns(Task.CompletedTask);
        _petRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>())).ReturnsAsync((Pet?)null);

        var matchDto = new MatchDto { Id = "match1", Status = MatchStatus.Rejected };
        _mapper.Setup(m => m.Map<MatchDto>(It.IsAny<Match>())).Returns(matchDto);

        var service = CreateService();
        var result = await service.UpdateMatchStatusAsync("match1", MatchStatus.Rejected);

        result.Status.Should().Be(MatchStatus.Rejected);
    }

    [Fact]
    public async Task GetPetMatchesAsync_ReturnsMatches()
    {
        var matches = new List<Match>
        {
            new() { Id = "m1", InitiatiorPetId = "pet1", TargetPetId = "pet2", Status = MatchStatus.Accepted, CreatedAt = DateTime.UtcNow },
            new() { Id = "m2", InitiatiorPetId = "pet3", TargetPetId = "pet1", Status = MatchStatus.Accepted, CreatedAt = DateTime.UtcNow }
        };

        var matchDtos = new List<MatchDto>
        {
            new() { Id = "m1", Status = MatchStatus.Accepted },
            new() { Id = "m2", Status = MatchStatus.Accepted }
        };

        _matchRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>())).ReturnsAsync(matches);
        _mapper.Setup(m => m.Map<IEnumerable<MatchDto>>(matches)).Returns(matchDtos);

        var service = CreateService();
        var result = await service.GetPetMatchesAsync("pet1");

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMatchByIdAsync_NotFound_ThrowsKeyNotFound()
    {
        _matchRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>())).ReturnsAsync((Match?)null);

        var service = CreateService();

        await service.Invoking(s => s.GetMatchByIdAsync("bad"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetPeerUserIdAsync_ReturnsOtherOwner_ForInitiator()
    {
        var matchDto = new MatchDto
        {
            Id = "match1",
            InitiatorPet = new PetDto { Id = "pet1", Name = "Pet_pet1", Species = "Dog", Breed = "Lab", OwnerId = "owner1" },
            TargetPet = new PetDto { Id = "pet2", Name = "Pet_pet2", Species = "Dog", Breed = "Lab", OwnerId = "owner2" },
            Status = MatchStatus.Accepted
        };

        var match = new Match
        {
            Id = "match1",
            InitiatiorPetId = "pet1",
            TargetPetId = "pet2",
            CreatedAt = DateTime.UtcNow
        };

        _matchRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>())).ReturnsAsync(match);
        _mapper.Setup(m => m.Map<MatchDto>(match)).Returns(matchDto);

        var service = CreateService();
        var result = await service.GetPeerUserIdAsync("match1", "owner1");

        result.Should().Be("owner2");
    }

    [Fact]
    public async Task GetPeerUserIdAsync_ThrowsUnauthorized_ForOutsider()
    {
        var matchDto = new MatchDto
        {
            Id = "match1",
            InitiatorPet = new PetDto { Id = "pet1", Name = "Pet_pet1", Species = "Dog", Breed = "Lab", OwnerId = "owner1" },
            TargetPet = new PetDto { Id = "pet2", Name = "Pet_pet2", Species = "Dog", Breed = "Lab", OwnerId = "owner2" },
            Status = MatchStatus.Accepted
        };

        var match = new Match
        {
            Id = "match1",
            InitiatiorPetId = "pet1",
            TargetPetId = "pet2",
            CreatedAt = DateTime.UtcNow
        };

        _matchRepo.Setup(r => r.GetBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>())).ReturnsAsync(match);
        _mapper.Setup(m => m.Map<MatchDto>(match)).Returns(matchDto);

        var service = CreateService();

        await service.Invoking(s => s.GetPeerUserIdAsync("match1", "intruder"))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteMatchAsync_ValidId_DeletesMatch()
    {
        var match = new Match { Id = "m1", InitiatiorPetId = "p1", TargetPetId = "p2", CreatedAt = DateTime.UtcNow };
        _matchRepo.Setup(r => r.GetByIdAsync("m1")).ReturnsAsync(match);
        _matchRepo.Setup(r => r.DeleteAsync(match)).Returns(Task.CompletedTask);

        var service = CreateService();
        await service.DeleteMatchAsync("m1");

        _matchRepo.Verify(r => r.DeleteAsync(match), Times.Once);
    }

    [Fact]
    public async Task DeleteMatchAsync_NotFound_ThrowsKeyNotFound()
    {
        _matchRepo.Setup(r => r.GetByIdAsync("bad")).ReturnsAsync((Match?)null);

        var service = CreateService();

        await service.Invoking(s => s.DeleteMatchAsync("bad"))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetWhoLikedYouAsync_FreeUser_ReturnsBlurredCards()
    {
        var myPet = CreatePet("myPet", "user1");
        myPet.Media = new List<PetMedia>();

        var likerPet = CreatePet("likerPet", "user2");
        likerPet.Name = "Buddy";
        likerPet.Breed = "Golden Retriever";
        likerPet.Media = new List<PetMedia>
        {
            new() { Id = "media1", FileName = "https://example.com/photo.jpg", Type = MediaType.Photo, PetId = "likerPet" }
        };

        _petRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>()))
            .ReturnsAsync(new List<Pet> { myPet });

        var pendingMatch = new Match
        {
            Id = "match1",
            InitiatiorPetId = "likerPet",
            TargetPetId = "myPet",
            InitiatiorPet = likerPet,
            TargetPet = myPet,
            Status = MatchStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        _matchRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>()))
            .ReturnsAsync(new List<Match> { pendingMatch });

        var service = CreateService();
        var result = await service.GetWhoLikedYouAsync("user1", SubscriptionPlan.Free);

        result.Should().HaveCount(1);
        result[0].IsBlurred.Should().BeTrue();
        result[0].PetName.Should().Be("B***");
        result[0].PetPhotoUrl.Should().BeNull();
        result[0].Breed.Should().BeNull();
        result[0].MatchId.Should().Be("match1");
    }

    [Fact]
    public async Task GetWhoLikedYouAsync_PaidUser_ReturnsFullDetails()
    {
        var myPet = CreatePet("myPet", "user1");

        var likerPet = CreatePet("likerPet", "user2");
        likerPet.Name = "Buddy";
        likerPet.Breed = "Golden Retriever";
        likerPet.Media = new List<PetMedia>
        {
            new() { Id = "media1", FileName = "https://example.com/photo.jpg", Type = MediaType.Photo, PetId = "likerPet" }
        };

        _petRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>()))
            .ReturnsAsync(new List<Pet> { myPet });

        var pendingMatch = new Match
        {
            Id = "match1",
            InitiatiorPetId = "likerPet",
            TargetPetId = "myPet",
            InitiatiorPet = likerPet,
            TargetPet = myPet,
            Status = MatchStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        _matchRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>()))
            .ReturnsAsync(new List<Match> { pendingMatch });

        var service = CreateService();
        var result = await service.GetWhoLikedYouAsync("user1", SubscriptionPlan.GoodBoy);

        result.Should().HaveCount(1);
        result[0].IsBlurred.Should().BeFalse();
        result[0].PetName.Should().Be("Buddy");
        result[0].PetPhotoUrl.Should().Be("https://example.com/photo.jpg");
        result[0].Breed.Should().Be("Golden Retriever");
    }

    [Fact]
    public async Task GetWhoLikedYouAsync_NoPendingLikes_ReturnsEmpty()
    {
        _petRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Pet>>()))
            .ReturnsAsync(new List<Pet> { CreatePet("myPet", "user1") });

        _matchRepo.Setup(r => r.FindBySpecificationAsync(It.IsAny<IQuerySpecification<Match>>()))
            .ReturnsAsync(new List<Match>());

        var service = CreateService();
        var result = await service.GetWhoLikedYouAsync("user1", SubscriptionPlan.Free);

        result.Should().BeEmpty();
    }
}
