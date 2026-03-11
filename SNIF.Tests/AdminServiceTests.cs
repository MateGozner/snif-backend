using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using SNIF.Application.Services;
using SNIF.Busniess.Services;
using SNIF.Core.Constants;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Infrastructure.Data;

namespace SNIF.Tests;

public class AdminServiceTests
{
    private SNIFContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SNIFContext>()
            .UseInMemoryDatabase("AdminTestDb_" + Guid.NewGuid())
            .Options;
        return new SNIFContext(options);
    }

    private static Mock<UserManager<User>> CreateMockUserManager()
    {
        var store = new Mock<IUserStore<User>>();
        return new Mock<UserManager<User>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<IPaymentService> CreateMockPaymentService()
    {
        return new Mock<IPaymentService>();
    }

    [Fact]
    public async Task GetUsers_ReturnsPaginatedResults()
    {
        using var context = CreateInMemoryContext();
        var mockUserManager = CreateMockUserManager();

        // Seed users
        for (int i = 1; i <= 15; i++)
        {
            context.Users.Add(new User
            {
                Id = $"user{i}",
                Name = $"User {i}",
                UserName = $"user{i}",
                Email = $"user{i}@test.com",
                CreatedAt = DateTime.UtcNow.AddDays(-i)
            });
        }
        await context.SaveChangesAsync();

        var paymentService = CreateMockPaymentService();
        var service = new AdminService(
            context,
            mockUserManager.Object,
            new EntitlementService(context),
            paymentService.Object);

        var filter = new AdminUserFilterDto { Page = 1, PageSize = 10 };
        var result = await service.GetUsersAsync(filter);

        result.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(15);
        result.TotalPages.Should().Be(2);
        result.Page.Should().Be(1);
        result.SupportSummary.FlaggedUsers.Should().Be(0);
    }

    [Fact]
    public async Task GetUsersAsync_IncludesSupportFlagsAndSummaryCounts()
    {
        using var context = CreateInMemoryContext();
        var mockUserManager = CreateMockUserManager();
        var paymentService = CreateMockPaymentService();

        var pendingUser = new User
        {
            Id = "support-pending",
            Name = "Pending Activation",
            UserName = "support-pending",
            Email = "pending@test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-4)
        };

        var pastDueUser = new User
        {
            Id = "support-past-due",
            Name = "Past Due",
            UserName = "support-past-due",
            Email = "pastdue@test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };

        context.Users.AddRange(pendingUser, pastDueUser);
        context.Pets.AddRange(
            new Pet
            {
                Id = "support-pet-1",
                OwnerId = pendingUser.Id,
                Name = "Milo",
                Species = "Dog",
                Breed = "Vizsla",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new Pet
            {
                Id = "support-pet-2",
                OwnerId = pendingUser.Id,
                Name = "Luna",
                Species = "Dog",
                Breed = "Vizsla",
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            });

        context.Subscriptions.AddRange(
            new Subscription
            {
                Id = "support-sub-1",
                UserId = pendingUser.Id,
                PlanId = SubscriptionPlan.GoodBoy,
                Status = SubscriptionStatus.Canceled,
                CurrentPeriodStart = DateTime.UtcNow.AddMonths(-1),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1),
                CancelAtPeriodEnd = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-1),
                UpdatedAt = DateTime.UtcNow
            },
            new Subscription
            {
                Id = "support-sub-2",
                UserId = pastDueUser.Id,
                PlanId = SubscriptionPlan.AlphaPack,
                Status = SubscriptionStatus.PastDue,
                CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
                CurrentPeriodEnd = DateTime.UtcNow.AddDays(5),
                CancelAtPeriodEnd = true,
                CreatedAt = DateTime.UtcNow.AddDays(-10),
                UpdatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var service = new AdminService(
            context,
            mockUserManager.Object,
            new EntitlementService(context),
            paymentService.Object);

        var result = await service.GetUsersAsync(new AdminUserFilterDto { Page = 1, PageSize = 10 });

        result.Items.Should().HaveCount(2);
        result.SupportSummary.FlaggedUsers.Should().Be(2);
        result.SupportSummary.PendingActivation.Should().Be(1);
        result.SupportSummary.PaidButStillFree.Should().Be(1);
        result.SupportSummary.PastDue.Should().Be(1);
        result.SupportSummary.CancelAtPeriodEnd.Should().Be(2);
        result.SupportSummary.DowngradeOrLockedPets.Should().Be(1);

        var pendingResult = result.Items.Single(user => user.Id == pendingUser.Id);
        pendingResult.SupportFlags.PendingActivation.Should().BeTrue();
        pendingResult.SupportFlags.PaidButStillFree.Should().BeTrue();
        pendingResult.SupportFlags.CancelAtPeriodEnd.Should().BeTrue();
        pendingResult.SupportFlags.DowngradeOrLockedPets.Should().BeTrue();
        pendingResult.SupportFlags.IssueCount.Should().BeGreaterThanOrEqualTo(4);

        var pastDueResult = result.Items.Single(user => user.Id == pastDueUser.Id);
        pastDueResult.SupportFlags.PastDue.Should().BeTrue();
        pastDueResult.SupportFlags.CancelAtPeriodEnd.Should().BeTrue();
        pastDueResult.SupportFlags.PendingActivation.Should().BeFalse();
    }

    [Fact]
    public async Task GetUsersAsync_FiltersByRequestedSupportIssue()
    {
        using var context = CreateInMemoryContext();
        var mockUserManager = CreateMockUserManager();
        var paymentService = CreateMockPaymentService();

        var pendingUser = new User
        {
            Id = "filter-pending",
            Name = "Pending Activation",
            UserName = "filter-pending",
            Email = "filter-pending@test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var cleanUser = new User
        {
            Id = "filter-clean",
            Name = "No Issues",
            UserName = "filter-clean",
            Email = "filter-clean@test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };

        context.Users.AddRange(pendingUser, cleanUser);
        context.Subscriptions.Add(new Subscription
        {
            Id = "filter-sub-1",
            UserId = pendingUser.Id,
            PlanId = SubscriptionPlan.GoodBoy,
            Status = SubscriptionStatus.Canceled,
            CurrentPeriodStart = DateTime.UtcNow.AddMonths(-1),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1),
            CancelAtPeriodEnd = false,
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new AdminService(
            context,
            mockUserManager.Object,
            new EntitlementService(context),
            paymentService.Object);

        var result = await service.GetUsersAsync(new AdminUserFilterDto
        {
            SupportIssue = "pendingActivation",
            Page = 1,
            PageSize = 10
        });

        result.Items.Should().ContainSingle();
        result.Items[0].Id.Should().Be(pendingUser.Id);
        result.SupportSummary.FlaggedUsers.Should().Be(1);
        result.SupportSummary.PendingActivation.Should().Be(1);
    }

    [Fact]
    public async Task SuspendUser_SetsSuspendedUntilAndReason()
    {
        using var context = CreateInMemoryContext();
        var mockUserManager = CreateMockUserManager();

        var admin = new User { Id = "admin1", Name = "Admin", UserName = "admin1" };
        var target = new User { Id = "target1", Name = "Target", UserName = "target1" };
        context.Users.AddRange(admin, target);
        await context.SaveChangesAsync();

        // Mock admin role check: admin has Admin role, target has no role
        mockUserManager.Setup(m => m.FindByIdAsync("target1")).ReturnsAsync(target);
        mockUserManager.Setup(m => m.FindByIdAsync("admin1")).ReturnsAsync(admin);
        mockUserManager.Setup(m => m.GetRolesAsync(target)).ReturnsAsync(new List<string>());
        mockUserManager.Setup(m => m.GetRolesAsync(admin)).ReturnsAsync(new List<string> { AppRoles.Admin });

        var paymentService = CreateMockPaymentService();
        var service = new AdminService(
            context,
            mockUserManager.Object,
            new EntitlementService(context),
            paymentService.Object);

        await service.SuspendUserAsync("target1", 7, "Bad behavior", "admin1");

        var user = await context.Users.FindAsync("target1");
        user!.SuspendedUntil.Should().NotBeNull();
        user.SuspendedUntil.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));
        user.BanReason.Should().Be("Bad behavior");
    }

    [Fact]
    public async Task BanUser_SetsIsBannedTrue()
    {
        using var context = CreateInMemoryContext();
        var mockUserManager = CreateMockUserManager();

        var admin = new User { Id = "admin1", Name = "Admin", UserName = "admin1" };
        var target = new User { Id = "target1", Name = "Target", UserName = "target1" };
        context.Users.AddRange(admin, target);
        await context.SaveChangesAsync();

        mockUserManager.Setup(m => m.FindByIdAsync("target1")).ReturnsAsync(target);
        mockUserManager.Setup(m => m.FindByIdAsync("admin1")).ReturnsAsync(admin);
        mockUserManager.Setup(m => m.GetRolesAsync(target)).ReturnsAsync(new List<string>());
        mockUserManager.Setup(m => m.GetRolesAsync(admin)).ReturnsAsync(new List<string> { AppRoles.Admin });

        var paymentService = CreateMockPaymentService();
        var service = new AdminService(
            context,
            mockUserManager.Object,
            new EntitlementService(context),
            paymentService.Object);

        await service.BanUserAsync("target1", "Violation", "admin1");

        var user = await context.Users.FindAsync("target1");
        user!.IsBanned.Should().BeTrue();
        user.BanReason.Should().Be("Violation");
    }

    [Fact]
    public async Task WarnUser_IncrementsWarningCount()
    {
        using var context = CreateInMemoryContext();
        var mockUserManager = CreateMockUserManager();

        var admin = new User { Id = "admin1", Name = "Admin", UserName = "admin1" };
        var target = new User { Id = "target1", Name = "Target", UserName = "target1", WarningCount = 2 };
        context.Users.AddRange(admin, target);
        await context.SaveChangesAsync();

        mockUserManager.Setup(m => m.FindByIdAsync("target1")).ReturnsAsync(target);
        mockUserManager.Setup(m => m.FindByIdAsync("admin1")).ReturnsAsync(admin);
        mockUserManager.Setup(m => m.GetRolesAsync(target)).ReturnsAsync(new List<string>());
        mockUserManager.Setup(m => m.GetRolesAsync(admin)).ReturnsAsync(new List<string> { AppRoles.Admin });

        var paymentService = CreateMockPaymentService();
        var service = new AdminService(
            context,
            mockUserManager.Object,
            new EntitlementService(context),
            paymentService.Object);

        await service.WarnUserAsync("target1", "admin1", "First warning");

        var user = await context.Users.FindAsync("target1");
        user!.WarningCount.Should().Be(3);
    }

    [Fact]
    public async Task GetUserDetailAsync_IncludesPaidButStillFreeAndLockedPetSignals()
    {
        using var context = CreateInMemoryContext();
        var mockUserManager = CreateMockUserManager();
        var paymentService = CreateMockPaymentService();

        var user = new User
        {
            Id = "user-support-1",
            Name = "Support Case",
            UserName = "support-case",
            Email = "support@test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        context.Users.Add(user);
        context.Pets.AddRange(
            new Pet
            {
                Id = "pet-1",
                OwnerId = user.Id,
                Name = "Milo",
                Species = "Dog",
                Breed = "Vizsla",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new Pet
            {
                Id = "pet-2",
                OwnerId = user.Id,
                Name = "Luna",
                Species = "Dog",
                Breed = "Vizsla",
                CreatedAt = DateTime.UtcNow.AddDays(-4)
            });
        context.Subscriptions.Add(new Subscription
        {
            Id = "sub-support-1",
            UserId = user.Id,
            PlanId = SubscriptionPlan.GoodBoy,
            Status = SubscriptionStatus.Canceled,
            CurrentPeriodStart = DateTime.UtcNow.AddMonths(-1),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(-1),
            CancelAtPeriodEnd = true,
            PaymentProviderSubscriptionId = "ls-sub-1",
            PaymentProviderCustomerId = "ls-cus-1",
            CreatedAt = DateTime.UtcNow.AddMonths(-1),
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string> { AppRoles.Support });
        paymentService
            .Setup(service => service.RefreshSubscriptionActivationStatus(user.Id))
            .ReturnsAsync(new SubscriptionActivationStatusDto
            {
                State = SubscriptionActivationState.Pending,
                Message = "Subscription found, but it is not currently active."
            });

        var service = new AdminService(
            context,
            mockUserManager.Object,
            new EntitlementService(context),
            paymentService.Object);

        var result = await service.GetUserDetailAsync(user.Id);

        result.SubscriptionSupport.Should().NotBeNull();
        result.SubscriptionSupport!.BillingPlan.Should().Be(SubscriptionPlan.GoodBoy);
        result.SubscriptionSupport.EffectivePlan.Should().Be(SubscriptionPlan.Free);
        result.SubscriptionSupport.EffectiveStatus.Should().Be(EntitlementStatus.Downgraded);
        result.SubscriptionSupport.PaidButStillFree.Should().BeTrue();
        result.SubscriptionSupport.ActivationState.Should().Be(SubscriptionActivationState.Pending);
        result.SubscriptionSupport.LockedPetCount.Should().Be(1);
        result.SubscriptionSupport.LockedPets.Select(p => p.PetId).Should().ContainSingle().Which.Should().Be("pet-2");
        result.SubscriptionSupport.PaymentProviderSubscriptionId.Should().Be("ls-sub-1");
        result.SubscriptionSupport.PaymentProviderCustomerId.Should().Be("ls-cus-1");
    }

    [Fact]
    public async Task GetUserDetailAsync_IncludesPastDueAndScheduledCancellationSignals()
    {
        using var context = CreateInMemoryContext();
        var mockUserManager = CreateMockUserManager();
        var paymentService = CreateMockPaymentService();

        var user = new User
        {
            Id = "user-support-2",
            Name = "Past Due",
            UserName = "past-due",
            Email = "pastdue@test.com",
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };

        context.Users.Add(user);
        context.Subscriptions.Add(new Subscription
        {
            Id = "sub-support-2",
            UserId = user.Id,
            PlanId = SubscriptionPlan.AlphaPack,
            Status = SubscriptionStatus.PastDue,
            CurrentPeriodStart = DateTime.UtcNow.AddDays(-10),
            CurrentPeriodEnd = DateTime.UtcNow.AddDays(5),
            CancelAtPeriodEnd = true,
            PaymentProviderSubscriptionId = "ls-sub-2",
            PaymentProviderCustomerId = "ls-cus-2",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        mockUserManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync(new List<string>());
        paymentService
            .Setup(service => service.RefreshSubscriptionActivationStatus(user.Id))
            .ReturnsAsync(new SubscriptionActivationStatusDto
            {
                State = SubscriptionActivationState.Activated,
                Message = "Subscription is already active."
            });

        var service = new AdminService(
            context,
            mockUserManager.Object,
            new EntitlementService(context),
            paymentService.Object);

        var result = await service.GetUserDetailAsync(user.Id);

        result.SubscriptionSupport.Should().NotBeNull();
        result.SubscriptionSupport!.SubscriptionStatus.Should().Be(SubscriptionStatus.PastDue);
        result.SubscriptionSupport.EffectiveStatus.Should().Be(EntitlementStatus.PastDueGrace);
        result.SubscriptionSupport.EffectivePlan.Should().Be(SubscriptionPlan.AlphaPack);
        result.SubscriptionSupport.CancelAtPeriodEnd.Should().BeTrue();
        result.SubscriptionSupport.DowngradeEffectiveAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(5), TimeSpan.FromMinutes(1));
        result.SubscriptionSupport.PaidButStillFree.Should().BeFalse();
    }
}
