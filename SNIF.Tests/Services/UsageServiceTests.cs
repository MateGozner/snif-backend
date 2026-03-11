using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SNIF.Busniess.Services;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Infrastructure.Data;

namespace SNIF.Tests.Services;

public class UsageServiceTests
{
    private static EntitlementSnapshotDto CreateEntitlement(
        SubscriptionPlan plan = SubscriptionPlan.Free,
        int totalPets = 0,
        bool isOverPetLimit = false)
    {
        return new EntitlementSnapshotDto
        {
            BillingPlan = plan,
            EffectivePlan = plan,
            EffectiveStatus = EntitlementStatus.Active,
            SubscriptionStatus = SubscriptionStatus.Active,
            Limits = PlanLimits.GetLimits(plan),
            TotalPets = totalPets,
            ActivePets = Math.Min(totalPets, PlanLimits.GetLimits(plan).MaxPets),
            LockedPets = isOverPetLimit ? Math.Max(0, totalPets - PlanLimits.GetLimits(plan).MaxPets) : 0,
            IsOverPetLimit = isOverPetLimit
        };
    }

    private SNIFContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SNIFContext>()
            .UseInMemoryDatabase("UsageTestDb_" + Guid.NewGuid())
            .Options;
        return new SNIFContext(options);
    }

    [Fact]
    public async Task RecordUsage_FirstTime_CreatesRecord()
    {
        using var context = CreateContext();
        var entitlementService = new Mock<IEntitlementService>();
        var service = new UsageService(context, entitlementService.Object);

        await service.RecordUsage("user1", UsageType.Like);

        var record = await context.UsageRecords
            .FirstOrDefaultAsync(r => r.UserId == "user1" && r.Type == UsageType.Like);

        record.Should().NotBeNull();
        record!.Count.Should().Be(1);
    }

    [Fact]
    public async Task RecordUsage_SecondTime_IncrementsCounter()
    {
        using var context = CreateContext();
        var entitlementService = new Mock<IEntitlementService>();
        var service = new UsageService(context, entitlementService.Object);

        await service.RecordUsage("user1", UsageType.Like);
        await service.RecordUsage("user1", UsageType.Like);

        var record = await context.UsageRecords
            .FirstOrDefaultAsync(r => r.UserId == "user1" && r.Type == UsageType.Like);

        record!.Count.Should().Be(2);
    }

    [Fact]
    public async Task GetDailyUsage_ReturnsCorrectCounts()
    {
        using var context = CreateContext();
        var today = DateTime.UtcNow.Date;

        context.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user1",
            Type = UsageType.Like,
            Count = 3,
            Date = today,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.GetEntitlementAsync("user1")).ReturnsAsync(CreateEntitlement());

        var service = new UsageService(context, entitlementService.Object);
        var result = await service.GetDailyUsage("user1", today);

        result.UserId.Should().Be("user1");
        result.UsageCounts[UsageType.Like].Should().Be(3);
        result.CurrentPlan.Should().Be(SubscriptionPlan.Free);
    }

    [Fact]
    public async Task CanPerformAction_WithinLimit_ReturnsAllowedViaPlanQuota()
    {
        using var context = CreateContext();
        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.GetEntitlementAsync("user1")).ReturnsAsync(CreateEntitlement());

        // Free plan: 5 daily likes. No usage yet → should be allowed.
        var service = new UsageService(context, entitlementService.Object);
        var result = await service.CanPerformAction("user1", UsageType.Like);

        result.Allowed.Should().BeTrue();
        result.Source.Should().Be(UsageSource.PlanQuota);
    }

    [Fact]
    public async Task CanPerformAction_AtLimit_NoCredits_ReturnsDenied()
    {
        using var context = CreateContext();
        var today = DateTime.UtcNow.Date;

        context.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user1",
            Type = UsageType.Like,
            Count = 5, // Free plan limit
            Date = today,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.GetEntitlementAsync("user1")).ReturnsAsync(CreateEntitlement());

        var service = new UsageService(context, entitlementService.Object);
        var result = await service.CanPerformAction("user1", UsageType.Like);

        result.Allowed.Should().BeFalse();
        result.Source.Should().Be(UsageSource.Denied);
    }

    [Fact]
    public async Task CanPerformAction_AlphaPack_UnlimitedLikes()
    {
        using var context = CreateContext();
        var today = DateTime.UtcNow.Date;

        context.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user1",
            Type = UsageType.Like,
            Count = 1000,
            Date = today,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.GetEntitlementAsync("user1")).ReturnsAsync(CreateEntitlement(SubscriptionPlan.AlphaPack));

        var service = new UsageService(context, entitlementService.Object);
        var result = await service.CanPerformAction("user1", UsageType.Like);

        result.Allowed.Should().BeTrue();
        result.Source.Should().Be(UsageSource.PlanQuota);
    }

    [Fact]
    public async Task CanPerformAction_VideoCall_FreePlan_ReturnsDenied()
    {
        using var context = CreateContext();
        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.GetEntitlementAsync("user1")).ReturnsAsync(CreateEntitlement());

        var service = new UsageService(context, entitlementService.Object);
        var result = await service.CanPerformAction("user1", UsageType.VideoCall);

        result.Allowed.Should().BeFalse();
        result.Source.Should().Be(UsageSource.Denied);
    }

    [Fact]
    public async Task CanPerformAction_SuperSniff_AtLimit_NoCredits_ReturnsDenied()
    {
        using var context = CreateContext();
        var today = DateTime.UtcNow.Date;

        context.UsageRecords.Add(new UsageRecord
        {
            Id = Guid.NewGuid().ToString(),
            UserId = "user1",
            Type = UsageType.SuperSniff,
            Count = 1,
            Date = today,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.GetEntitlementAsync("user1")).ReturnsAsync(CreateEntitlement());

        var service = new UsageService(context, entitlementService.Object);
        var result = await service.CanPerformAction("user1", UsageType.SuperSniff);

        result.Allowed.Should().BeFalse();
        result.Source.Should().Be(UsageSource.Denied);
    }

    [Fact]
    public async Task GetLimitsForPlan_ReturnsCorrectValues()
    {
        using var context = CreateContext();
        var entitlementService = new Mock<IEntitlementService>();
        var service = new UsageService(context, entitlementService.Object);

        var freeLimits = service.GetLimitsForPlan(SubscriptionPlan.Free);
        freeLimits.MaxPets.Should().Be(1);
        freeLimits.DailyLikes.Should().Be(5);
        freeLimits.VideoCallEnabled.Should().BeFalse();
        freeLimits.HasAds.Should().BeTrue();

        var alphaLimits = service.GetLimitsForPlan(SubscriptionPlan.AlphaPack);
        alphaLimits.MaxPets.Should().Be(5);
        alphaLimits.UnlimitedLikes.Should().BeTrue();
        alphaLimits.VideoCallEnabled.Should().BeTrue();
        alphaLimits.HasAds.Should().BeFalse();

        var goodBoyLimits = service.GetLimitsForPlan(SubscriptionPlan.GoodBoy);
        goodBoyLimits.MaxPets.Should().Be(3);
        goodBoyLimits.DailyLikes.Should().Be(25);
    }

    [Fact]
    public async Task CanPerformAction_PetCreation_WhenDowngradedOverLimit_ReturnsDenied()
    {
        using var context = CreateContext();
        var entitlementService = new Mock<IEntitlementService>();
        entitlementService.Setup(s => s.GetEntitlementAsync("user1"))
            .ReturnsAsync(CreateEntitlement(SubscriptionPlan.Free, totalPets: 2, isOverPetLimit: true));

        var service = new UsageService(context, entitlementService.Object);
        var result = await service.CanPerformAction("user1", UsageType.PetCreation);

        result.Allowed.Should().BeFalse();
        result.Source.Should().Be(UsageSource.Denied);
    }
}
