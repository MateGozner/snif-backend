using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SNIF.Busniess.Services.Matching;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces.Matching;
using SNIF.Core.Models;
using SNIF.Core.Interfaces;
using SNIF.Core.Models.Matching;

namespace SNIF.Tests;

public class MatchingPipelineTests
{
    private readonly Mock<ILogger<HardFilterStage>> _hardFilterLogger = new();
    private readonly Mock<ILogger<ScoringStage>> _scoringLogger = new();
    private readonly Mock<ILogger<RankingStage>> _rankingLogger = new();
    private readonly Mock<ILogger<MatchPipeline>> _pipelineLogger = new();
    private readonly Mock<IEntitlementService> _entitlementServiceMock = new();

    public MatchingPipelineTests()
    {
        _entitlementServiceMock
            .Setup(s => s.GetEntitlementAsync(It.IsAny<string>()))
            .ReturnsAsync((string _) => new EntitlementSnapshotDto
            {
                BillingPlan = SubscriptionPlan.GoodBoy,
                EffectivePlan = SubscriptionPlan.GoodBoy,
                EffectiveStatus = EntitlementStatus.Active,
                SubscriptionStatus = SubscriptionStatus.Active,
                Limits = PlanLimits.GetLimits(SubscriptionPlan.GoodBoy),
                TotalPets = 1,
                ActivePets = 1,
                LockedPets = 0,
                IsOverPetLimit = false,
                PetStates = Array.Empty<PetEntitlementStateDto>(),
                LockedPetIds = Array.Empty<string>()
            });
    }

    private Pet CreatePet(string id, string ownerId, string species = "Dog", string breed = "Labrador",
        int age = 3, Gender gender = Gender.Male, double lat = 47.5, double lon = 19.0)
    {
        return new Pet
        {
            Id = id,
            OwnerId = ownerId,
            Name = $"Pet_{id}",
            Species = species,
            Breed = breed,
            Age = age,
            Gender = gender,
            Location = new Location { Id = 1, Latitude = lat, Longitude = lon }
        };
    }

    private User CreateOwner(string id, double searchRadius = 50.0)
    {
        return new User
        {
            Id = id,
            Name = $"Owner_{id}",
            UserName = $"owner_{id}",
            Preferences = new UserPreferences
            {
                Id = 1,
                UserId = id,
                SearchRadius = searchRadius,
                NotificationSettings = new NotificationSettings()
            }
        };
    }

    [Fact]
    public async Task HardFilter_ExcludesOwnPets()
    {
        var stage = new HardFilterStage(_hardFilterLogger.Object, _entitlementServiceMock.Object);
        var owner = CreateOwner("owner1");
        var sourcePet = CreatePet("pet1", "owner1");
        var ownPet = CreatePet("pet2", "owner1"); // same owner
        var otherPet = CreatePet("pet3", "owner2");

        var context = new MatchPipelineContext
        {
            SourcePet = sourcePet,
            Owner = owner,
            ExistingMatchPetIds = new HashSet<string>(),
            Candidates = new List<MatchCandidate>
            {
                new() { Pet = sourcePet },
                new() { Pet = ownPet },
                new() { Pet = otherPet }
            }
        };

        var result = await stage.ExecuteAsync(context);

        // Source pet should be filtered (self-pet)
        result.Candidates.First(c => c.Pet.Id == "pet1").IsFiltered.Should().BeTrue();
        result.Candidates.First(c => c.Pet.Id == "pet1").RejectReason.Should().Be(RejectReason.SelfPet);

        // Same-owner pet should be filtered
        result.Candidates.First(c => c.Pet.Id == "pet2").IsFiltered.Should().BeTrue();
        result.Candidates.First(c => c.Pet.Id == "pet2").RejectReason.Should().Be(RejectReason.SameOwner);

        // Other pet should pass
        result.Candidates.First(c => c.Pet.Id == "pet3").IsFiltered.Should().BeFalse();
    }

    [Fact]
    public async Task HardFilter_ExcludesExistingMatches()
    {
        var stage = new HardFilterStage(_hardFilterLogger.Object, _entitlementServiceMock.Object);
        var owner = CreateOwner("owner1");
        var sourcePet = CreatePet("pet1", "owner1");
        var matchedPet = CreatePet("pet2", "owner2");
        var unmatchedPet = CreatePet("pet3", "owner3");

        var context = new MatchPipelineContext
        {
            SourcePet = sourcePet,
            Owner = owner,
            ExistingMatchPetIds = new HashSet<string> { "pet2" }, // already matched
            Candidates = new List<MatchCandidate>
            {
                new() { Pet = matchedPet },
                new() { Pet = unmatchedPet }
            }
        };

        var result = await stage.ExecuteAsync(context);

        result.Candidates.First(c => c.Pet.Id == "pet2").IsFiltered.Should().BeTrue();
        result.Candidates.First(c => c.Pet.Id == "pet2").RejectReason.Should().Be(RejectReason.ExistingMatch);
        result.Candidates.First(c => c.Pet.Id == "pet3").IsFiltered.Should().BeFalse();
    }

    [Fact]
    public async Task ScoringStage_AssignsScoresToPassingCandidates()
    {
        var mockScorer = new Mock<IMatchScoringFunction>();
        mockScorer.Setup(s => s.Name).Returns("TestScorer");
        mockScorer.Setup(s => s.Weight).Returns(1.0);
        mockScorer.Setup(s => s.Score(It.IsAny<MatchCandidate>(), It.IsAny<MatchPipelineContext>()))
            .Returns(0.8);

        var stage = new ScoringStage(new[] { mockScorer.Object }, _scoringLogger.Object);

        var sourcePet = CreatePet("pet1", "owner1");
        var candidatePet = CreatePet("pet2", "owner2");
        var filteredPet = CreatePet("pet3", "owner3");

        var context = new MatchPipelineContext
        {
            SourcePet = sourcePet,
            Owner = CreateOwner("owner1"),
            Candidates = new List<MatchCandidate>
            {
                new() { Pet = candidatePet },
                new() { Pet = filteredPet, IsFiltered = true }
            }
        };

        var result = await stage.ExecuteAsync(context);

        var passing = result.Candidates.First(c => c.Pet.Id == "pet2");
        passing.Score.Should().BeApproximately(0.8, 0.001);
        passing.ScoreBreakdown.Should().ContainKey("TestScorer");

        // Filtered candidate should not have a score
        var filtered = result.Candidates.First(c => c.Pet.Id == "pet3");
        filtered.Score.Should().Be(0);
    }

    [Fact]
    public async Task RankingStage_OrdersByScoreDescending()
    {
        var stage = new RankingStage(_rankingLogger.Object);

        var context = new MatchPipelineContext
        {
            SourcePet = CreatePet("source", "owner1"),
            Owner = CreateOwner("owner1"),
            Page = 1,
            PageSize = 20,
            Candidates = new List<MatchCandidate>
            {
                new() { Pet = CreatePet("low", "o2"), Score = 0.3 },
                new() { Pet = CreatePet("high", "o3"), Score = 0.9 },
                new() { Pet = CreatePet("mid", "o4"), Score = 0.6 }
            }
        };

        var result = await stage.ExecuteAsync(context);

        var unfiltered = result.Candidates.Where(c => !c.IsFiltered).ToList();
        unfiltered.Should().HaveCount(3);
        unfiltered[0].Pet.Id.Should().Be("high");
        unfiltered[1].Pet.Id.Should().Be("mid");
        unfiltered[2].Pet.Id.Should().Be("low");
    }

    [Fact]
    public async Task FullPipeline_FilterScoreRank()
    {
        // Set up scorers
        var mockScorer = new Mock<IMatchScoringFunction>();
        mockScorer.Setup(s => s.Name).Returns("TestScorer");
        mockScorer.Setup(s => s.Weight).Returns(1.0);
        mockScorer.Setup(s => s.Score(It.IsAny<MatchCandidate>(), It.IsAny<MatchPipelineContext>()))
            .Returns((MatchCandidate c, MatchPipelineContext _) =>
                c.Pet.Id == "good" ? 0.9 : 0.4);

        var stages = new IMatchStage[]
        {
            new HardFilterStage(_hardFilterLogger.Object, _entitlementServiceMock.Object),
            new ScoringStage(new[] { mockScorer.Object }, _scoringLogger.Object),
            new RankingStage(_rankingLogger.Object)
        };

        var pipeline = new MatchPipeline(stages, _pipelineLogger.Object);

        var owner = CreateOwner("owner1");
        var sourcePet = CreatePet("source", "owner1");
        var selfPet = CreatePet("source", "owner1"); // self
        var sameOwnerPet = CreatePet("same", "owner1"); // same owner
        var goodPet = CreatePet("good", "owner2");
        var okPet = CreatePet("ok", "owner3");

        var allPets = new[] { selfPet, sameOwnerPet, goodPet, okPet };

        var result = await pipeline.ExecuteAsync(
            sourcePet, allPets, owner,
            new HashSet<string>());

        // Self and same-owner should be filtered; 2 candidates remain
        result.Should().HaveCount(2);
        result[0].Pet.Id.Should().Be("good"); // highest score
        result[1].Pet.Id.Should().Be("ok");
    }
}
