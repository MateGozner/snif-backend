using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using SNIF.API.Filters;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using System.Security.Claims;
using System.Text.Json;

namespace SNIF.Tests.Filters
{
    public class UsageEnforcementFilterTests
    {
        private readonly Mock<IUsageService> _usageServiceMock = new();
        private readonly Mock<IEntitlementService> _entitlementServiceMock = new();

        private static EntitlementSnapshotDto CreateEntitlement(
            SubscriptionPlan plan = SubscriptionPlan.Free,
            int totalPets = 1,
            bool locked = false)
        {
            var petStates = locked
                ? new[]
                {
                    new PetEntitlementStateDto
                    {
                        PetId = "pet-locked",
                        PetName = "Locked Pet",
                        CreatedAt = DateTime.UtcNow,
                        IsLocked = true,
                        LockReason = "Pet locked until subscription is upgraded."
                    }
                }
                : Array.Empty<PetEntitlementStateDto>();

            return new EntitlementSnapshotDto
            {
                BillingPlan = plan,
                EffectivePlan = plan,
                EffectiveStatus = EntitlementStatus.Active,
                SubscriptionStatus = SubscriptionStatus.Active,
                Limits = PlanLimits.GetLimits(plan),
                TotalPets = totalPets,
                ActivePets = locked ? totalPets - 1 : totalPets,
                LockedPets = locked ? 1 : 0,
                IsOverPetLimit = locked,
                PetStates = petStates,
                LockedPetIds = petStates.Select(p => p.PetId).ToArray()
            };
        }

        private ActionExecutingContext CreateContext(string? userId = "user-1")
        {
            var services = new ServiceCollection();
            services.AddSingleton(_usageServiceMock.Object);
            services.AddSingleton(_entitlementServiceMock.Object);
            var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };

            if (userId != null)
            {
                var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId) };
                httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
            }

            var actionContext = new ActionContext(
                httpContext,
                new RouteData(),
                new ActionDescriptor());

            return new ActionExecutingContext(
                actionContext,
                new List<IFilterMetadata>(),
                new Dictionary<string, object?>(),
                new object());
        }

        private ActionExecutionDelegate CreateNextDelegate(bool called = false)
        {
            var executedContext = new ActionExecutedContext(
                new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                new List<IFilterMetadata>(),
                new object());

            return () => Task.FromResult(executedContext);
        }

        [Fact]
        public async Task LikeAction_WhenUnderLimit_ProceedsToNext()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.Like))
                .ReturnsAsync(new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota });

            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext();
            var nextCalled = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(
                    new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                    new List<IFilterMetadata>(), new object()));
            });

            Assert.True(nextCalled);
            Assert.Null(context.Result);
        }

        [Fact]
        public async Task LikeAction_WhenAtLimit_Returns403WithCorrectBody()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.Like))
                .ReturnsAsync(new UsageCheckResult { Allowed = false, Source = UsageSource.Denied });
            _entitlementServiceMock.Setup(s => s.GetEntitlementAsync("user-1"))
                .ReturnsAsync(CreateEntitlement(totalPets: 1));
            _usageServiceMock.Setup(s => s.GetDailyUsage("user-1", It.IsAny<DateTime>()))
                .ReturnsAsync(new UsageResponseDto
                {
                    UserId = "user-1",
                    Date = DateTime.UtcNow.Date,
                    CurrentPlan = SubscriptionPlan.Free,
                    CurrentLimits = PlanLimits.GetLimits(SubscriptionPlan.Free),
                    UsageCounts = new Dictionary<UsageType, int> { [UsageType.Like] = 5 }
                });

            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, CreateNextDelegate());

            var result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(403, result.StatusCode);

            var json = JsonSerializer.Serialize(result.Value);
            var body = JsonDocument.Parse(json).RootElement;
            Assert.Equal("DailyLikeLimit", body.GetProperty("error").GetString());
            Assert.Equal(5, body.GetProperty("limit").GetInt32());
            Assert.Equal(5, body.GetProperty("used").GetInt32());
            Assert.Equal("Free", body.GetProperty("plan").GetString());
            Assert.Equal("/pricing", body.GetProperty("upgradeUrl").GetString());
        }

        [Fact]
        public async Task PetCreation_WhenAtLimit_Returns403WithPetLimitError()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.PetCreation))
                .ReturnsAsync(new UsageCheckResult { Allowed = false, Source = UsageSource.Denied });
            _entitlementServiceMock.Setup(s => s.GetEntitlementAsync("user-1"))
                .ReturnsAsync(CreateEntitlement(totalPets: 1));

            var filter = new UsageEnforcementFilter(UsageType.PetCreation);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, CreateNextDelegate());

            var result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(403, result.StatusCode);

            var json = JsonSerializer.Serialize(result.Value);
            var body = JsonDocument.Parse(json).RootElement;
            Assert.Equal("PetLimit", body.GetProperty("error").GetString());
            Assert.Equal(1, body.GetProperty("limit").GetInt32());
            Assert.Equal(1, body.GetProperty("current").GetInt32());
            Assert.Equal("Free", body.GetProperty("plan").GetString());
        }

        [Fact]
        public async Task SuperSniff_WhenAtLimit_Returns403WithSuperSniffError()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.SuperSniff))
                .ReturnsAsync(new UsageCheckResult { Allowed = false, Source = UsageSource.Denied });
            _entitlementServiceMock.Setup(s => s.GetEntitlementAsync("user-1"))
                .ReturnsAsync(CreateEntitlement(totalPets: 1));
            _usageServiceMock.Setup(s => s.GetDailyUsage("user-1", It.IsAny<DateTime>()))
                .ReturnsAsync(new UsageResponseDto
                {
                    UserId = "user-1",
                    Date = DateTime.UtcNow.Date,
                    CurrentPlan = SubscriptionPlan.Free,
                    CurrentLimits = PlanLimits.GetLimits(SubscriptionPlan.Free),
                    UsageCounts = new Dictionary<UsageType, int> { [UsageType.SuperSniff] = 1 }
                });

            var filter = new UsageEnforcementFilter(UsageType.SuperSniff);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, CreateNextDelegate());

            var result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(403, result.StatusCode);

            var json = JsonSerializer.Serialize(result.Value);
            var body = JsonDocument.Parse(json).RootElement;
            Assert.Equal("DailySuperSniffLimit", body.GetProperty("error").GetString());
            Assert.Equal(1, body.GetProperty("limit").GetInt32());
            Assert.Equal(1, body.GetProperty("used").GetInt32());
            Assert.Equal("Free", body.GetProperty("plan").GetString());
        }

        [Fact]
        public async Task LikeAction_GoodBoyPlan_Returns403WithGoodBoyLimits()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.Like))
                .ReturnsAsync(new UsageCheckResult { Allowed = false, Source = UsageSource.Denied });
            _entitlementServiceMock.Setup(s => s.GetEntitlementAsync("user-1"))
                .ReturnsAsync(CreateEntitlement(SubscriptionPlan.GoodBoy, totalPets: 1));
            _usageServiceMock.Setup(s => s.GetDailyUsage("user-1", It.IsAny<DateTime>()))
                .ReturnsAsync(new UsageResponseDto
                {
                    UserId = "user-1",
                    Date = DateTime.UtcNow.Date,
                    CurrentPlan = SubscriptionPlan.GoodBoy,
                    CurrentLimits = PlanLimits.GetLimits(SubscriptionPlan.GoodBoy),
                    UsageCounts = new Dictionary<UsageType, int> { [UsageType.Like] = 25 }
                });

            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, CreateNextDelegate());

            var result = Assert.IsType<ObjectResult>(context.Result);
            Assert.Equal(403, result.StatusCode);

            var json = JsonSerializer.Serialize(result.Value);
            var body = JsonDocument.Parse(json).RootElement;
            Assert.Equal("DailyLikeLimit", body.GetProperty("error").GetString());
            Assert.Equal(25, body.GetProperty("limit").GetInt32());
            Assert.Equal("GoodBoy", body.GetProperty("plan").GetString());
        }

        [Fact]
        public async Task NoUserId_ReturnsUnauthorized()
        {
            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext(userId: null);

            await filter.OnActionExecutionAsync(context, CreateNextDelegate());

            Assert.IsType<UnauthorizedResult>(context.Result);
        }

        [Fact]
        public async Task PetCreation_GoodBoyPlan_Returns403With3PetLimit()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.PetCreation))
                .ReturnsAsync(new UsageCheckResult { Allowed = false, Source = UsageSource.Denied });
            _entitlementServiceMock.Setup(s => s.GetEntitlementAsync("user-1"))
                .ReturnsAsync(CreateEntitlement(SubscriptionPlan.GoodBoy, totalPets: 3));

            var filter = new UsageEnforcementFilter(UsageType.PetCreation);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, CreateNextDelegate());

            var result = Assert.IsType<ObjectResult>(context.Result);
            var json = JsonSerializer.Serialize(result.Value);
            var body = JsonDocument.Parse(json).RootElement;
            Assert.Equal("PetLimit", body.GetProperty("error").GetString());
            Assert.Equal(3, body.GetProperty("limit").GetInt32());
        }

        [Fact]
        public async Task AlphaPack_LikeAction_AlwaysProceeds()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.Like))
                .ReturnsAsync(new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota });

            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext();
            var nextCalled = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(
                    new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                    new List<IFilterMetadata>(), new object()));
            });

            Assert.True(nextCalled);
        }

        [Fact]
        public async Task RecordsUsage_AfterSuccessfulAction()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.Like))
                .ReturnsAsync(new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota });

            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, () =>
            {
                return Task.FromResult(new ActionExecutedContext(
                    new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                    new List<IFilterMetadata>(), new object()));
            });

            _usageServiceMock.Verify(s => s.RecordUsage("user-1", UsageType.Like), Times.Once);
        }

        [Fact]
        public async Task DoesNotRecordUsage_WhenActionThrowsException()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.Like))
                .ReturnsAsync(new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota });

            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, () =>
            {
                var executedContext = new ActionExecutedContext(
                    new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                    new List<IFilterMetadata>(), new object())
                {
                    Exception = new InvalidOperationException("Something failed")
                };
                return Task.FromResult(executedContext);
            });

            _usageServiceMock.Verify(s => s.RecordUsage(It.IsAny<string>(), It.IsAny<UsageType>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotRecordUsage_WhenActionIsCanceled()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.Like))
                .ReturnsAsync(new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota });

            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, () =>
            {
                var executedContext = new ActionExecutedContext(
                    new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()),
                    new List<IFilterMetadata>(), new object())
                {
                    Canceled = true
                };
                return Task.FromResult(executedContext);
            });

            _usageServiceMock.Verify(s => s.RecordUsage(It.IsAny<string>(), It.IsAny<UsageType>()), Times.Never);
        }

        [Fact]
        public async Task DoesNotRecordUsage_WhenLimitReached()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.Like))
                .ReturnsAsync(new UsageCheckResult { Allowed = false, Source = UsageSource.Denied });
            _entitlementServiceMock.Setup(s => s.GetEntitlementAsync("user-1"))
                .ReturnsAsync(CreateEntitlement());
            _usageServiceMock.Setup(s => s.GetDailyUsage("user-1", It.IsAny<DateTime>()))
                .ReturnsAsync(new UsageResponseDto
                {
                    UserId = "user-1",
                    Date = DateTime.UtcNow.Date,
                    CurrentPlan = SubscriptionPlan.Free,
                    CurrentLimits = PlanLimits.GetLimits(SubscriptionPlan.Free),
                    UsageCounts = new Dictionary<UsageType, int> { [UsageType.Like] = 5 }
                });

            var filter = new UsageEnforcementFilter(UsageType.Like);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, CreateNextDelegate());

            Assert.Equal(403, ((ObjectResult)context.Result!).StatusCode);
            _usageServiceMock.Verify(s => s.RecordUsage(It.IsAny<string>(), It.IsAny<UsageType>()), Times.Never);
        }

        [Fact]
        public void PlanLimits_FreePlan_Has5DailyLikesAnd1Pet()
        {
            var limits = PlanLimits.GetLimits(SubscriptionPlan.Free);
            Assert.Equal(5, limits.DailyLikes);
            Assert.Equal(1, limits.MaxPets);
            Assert.Equal(5, limits.SearchRadiusKm);
        }

        [Fact]
        public void PlanLimits_GoodBoy_Has25DailyLikesAnd3Pets()
        {
            var limits = PlanLimits.GetLimits(SubscriptionPlan.GoodBoy);
            Assert.Equal(25, limits.DailyLikes);
            Assert.Equal(3, limits.MaxPets);
            Assert.Equal(50, limits.SearchRadiusKm);
        }

        [Fact]
        public void PlanLimits_AlphaPack_HasUnlimitedLikesAnd500kmRadius()
        {
            var limits = PlanLimits.GetLimits(SubscriptionPlan.AlphaPack);
            Assert.True(limits.UnlimitedLikes);
            Assert.Equal(500, limits.SearchRadiusKm);
            Assert.Equal(5, limits.MaxPets);
        }

        [Fact]
        public async Task PetCreation_WhenOverLimit_IncludesLockedPets()
        {
            _usageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.PetCreation))
                .ReturnsAsync(new UsageCheckResult { Allowed = false, Source = UsageSource.Denied });
            _entitlementServiceMock.Setup(s => s.GetEntitlementAsync("user-1"))
                .ReturnsAsync(CreateEntitlement(totalPets: 2, locked: true));

            var filter = new UsageEnforcementFilter(UsageType.PetCreation);
            var context = CreateContext();

            await filter.OnActionExecutionAsync(context, CreateNextDelegate());

            var result = Assert.IsType<ObjectResult>(context.Result);
            var json = JsonSerializer.Serialize(result.Value);
            var body = JsonDocument.Parse(json).RootElement;
            Assert.Equal(1, body.GetProperty("lockedPets").GetArrayLength());
        }
    }
}
