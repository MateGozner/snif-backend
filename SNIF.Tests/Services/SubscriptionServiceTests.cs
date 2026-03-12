using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SNIF.Busniess.Services;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Infrastructure.Data;

namespace SNIF.Tests.Services;

public class SubscriptionServiceTests
{
    private SNIFContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SNIFContext>()
            .UseInMemoryDatabase("SubTestDb_" + Guid.NewGuid())
            .Options;
        return new SNIFContext(options);
    }

    [Fact]
    public async Task CreateOrUpdateSubscription_NewUser_CreatesSubscription()
    {
        using var context = CreateContext();
        var service = new SubscriptionService(context);

        var result = await service.CreateOrUpdateSubscription("user1", SubscriptionPlan.GoodBoy, "sub_1", "cus_1");

        result.Should().NotBeNull();
        result.UserId.Should().Be("user1");
        result.PlanId.Should().Be(SubscriptionPlan.GoodBoy);
        result.Status.Should().Be(SubscriptionStatus.Active);

        var stored = await context.Subscriptions.FirstOrDefaultAsync(s => s.UserId == "user1");
        stored.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSubscription_ActiveSubscription_ReturnsDto()
    {
        using var context = CreateContext();
        context.Subscriptions.Add(new Subscription
        {
            Id = "sub1",
            UserId = "user1",
            PlanId = SubscriptionPlan.GoodBoy,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionService(context);
        var result = await service.GetSubscription("user1");

        result.Should().NotBeNull();
        result!.PlanId.Should().Be(SubscriptionPlan.GoodBoy);
        result.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public async Task GetSubscription_NoSubscription_ReturnsNull()
    {
        using var context = CreateContext();
        var service = new SubscriptionService(context);

        var result = await service.GetSubscription("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CancelSubscription_ActiveSub_SetsCancelAtPeriodEnd()
    {
        using var context = CreateContext();
        context.Subscriptions.Add(new Subscription
        {
            Id = "sub1",
            UserId = "user1",
            PlanId = SubscriptionPlan.GoodBoy,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionService(context);
        await service.CancelSubscription("user1");

        var sub = await context.Subscriptions.FirstAsync(s => s.UserId == "user1");
        sub.CancelAtPeriodEnd.Should().BeTrue();
    }

    [Fact]
    public async Task CancelSubscription_NoActiveSub_Throws()
    {
        using var context = CreateContext();
        var service = new SubscriptionService(context);

        await service.Invoking(s => s.CancelSubscription("user1"))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateOrUpdateSubscription_Upgrade_UpdatesExistingPlan()
    {
        using var context = CreateContext();
        context.Subscriptions.Add(new Subscription
        {
            Id = "sub1",
            UserId = "user1",
            PlanId = SubscriptionPlan.GoodBoy,
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionService(context);
        var result = await service.CreateOrUpdateSubscription("user1", SubscriptionPlan.AlphaPack, "sub_2", "cus_1");

        result.PlanId.Should().Be(SubscriptionPlan.AlphaPack);
        result.Status.Should().Be(SubscriptionStatus.Active);

        var count = await context.Subscriptions.CountAsync(s => s.UserId == "user1");
        count.Should().Be(1); // should update, not create new
    }

    [Fact]
    public async Task HandleSubscriptionDeleted_SetsCanceled()
    {
        using var context = CreateContext();
        context.Subscriptions.Add(new Subscription
        {
            Id = "sub1",
            UserId = "user1",
            PlanId = SubscriptionPlan.GoodBoy,
            PaymentProviderSubscriptionId = "provider_sub_1",
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = DateTime.UtcNow,
            CurrentPeriodEnd = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new SubscriptionService(context);
        await service.HandleSubscriptionDeleted("provider_sub_1");

        var sub = await context.Subscriptions.FirstAsync(s => s.PaymentProviderSubscriptionId == "provider_sub_1");
        sub.Status.Should().Be(SubscriptionStatus.Canceled);
    }

    [Fact]
    public async Task HandleSubscriptionUpdated_UpdatesFields()
    {
        using var context = CreateContext();
        var periodStart = DateTime.UtcNow;
        var periodEnd = DateTime.UtcNow.AddMonths(1);

        context.Subscriptions.Add(new Subscription
        {
            Id = "sub1",
            UserId = "user1",
            PlanId = SubscriptionPlan.GoodBoy,
            PaymentProviderSubscriptionId = "provider_sub_1",
            Status = SubscriptionStatus.Active,
            CurrentPeriodStart = periodStart,
            CurrentPeriodEnd = periodEnd,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var newStart = DateTime.UtcNow.AddMonths(1);
        var newEnd = DateTime.UtcNow.AddMonths(2);

        var service = new SubscriptionService(context);
        await service.HandleSubscriptionUpdated("provider_sub_1", SubscriptionStatus.Active, newStart, newEnd, true);

        var sub = await context.Subscriptions.FirstAsync(s => s.PaymentProviderSubscriptionId == "provider_sub_1");
        sub.CurrentPeriodStart.Should().Be(newStart);
        sub.CurrentPeriodEnd.Should().Be(newEnd);
        sub.CancelAtPeriodEnd.Should().BeTrue();
    }
}
