using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace SNIF.Tests.Controllers;

public class SuperSniffControllerTests : IClassFixture<SuperSniffWebApplicationFactory>
{
    private readonly SuperSniffWebApplicationFactory _factory;

    public SuperSniffControllerTests(SuperSniffWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static string GenerateTestJwt(string userId, string role = "User")
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(CustomWebApplicationFactory.JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userId),
            new Claim(ClaimTypes.Role, role)
        };
        var token = new JwtSecurityToken(
            issuer: "http://localhost:3000",
            audience: "http://localhost:3000",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task CreateSuperSniff_WhenLimitReached_ReturnsStructured403()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateTestJwt("user-1"));

        _factory.UsageServiceMock.Reset();
        _factory.MatchServiceMock.Reset();
        _factory.UsageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.SuperSniff))
            .ReturnsAsync(new UsageCheckResult { Allowed = false, Source = UsageSource.Denied });
        _factory.UsageServiceMock.Setup(s => s.GetDailyUsage("user-1", It.IsAny<DateTime>()))
            .ReturnsAsync(new UsageResponseDto
            {
                UserId = "user-1",
                Date = DateTime.UtcNow.Date,
                CurrentPlan = SubscriptionPlan.Free,
                CurrentLimits = PlanLimits.GetLimits(SubscriptionPlan.Free),
                UsageCounts = new Dictionary<UsageType, int> { [UsageType.SuperSniff] = 1 }
            });

        var response = await client.PostAsJsonAsync("/api/matches/super-sniff", new
        {
            InitiatorPetId = "pet-1",
            TargetPetId = "pet-2",
            MatchPurpose = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var body = await response.Content.ReadFromJsonAsync<LimitResponse>();
        body.Should().NotBeNull();
        body!.Error.Should().Be("DailySuperSniffLimit");
        body.Limit.Should().Be(1);
        body.Used.Should().Be(1);

        _factory.MatchServiceMock.Verify(s => s.CreateMatchAsync(It.IsAny<string>(), It.IsAny<CreateMatchDto>()), Times.Never);
        _factory.UsageServiceMock.Verify(s => s.RecordUsage(It.IsAny<string>(), It.IsAny<UsageType>()), Times.Never);
    }

    [Fact]
    public async Task CreateSuperSniff_WhenAllowed_CreatesMatchAndRecordsUsage()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateTestJwt("user-1"));

        _factory.UsageServiceMock.Reset();
        _factory.MatchServiceMock.Reset();
        _factory.UsageServiceMock.Setup(s => s.CanPerformAction("user-1", UsageType.SuperSniff))
            .ReturnsAsync(new UsageCheckResult { Allowed = true, Source = UsageSource.PlanQuota });
        _factory.MatchServiceMock.Setup(s => s.CreateMatchAsync("user-1", It.IsAny<CreateMatchDto>()))
            .ReturnsAsync(new MatchDto
            {
                Id = "match-1",
                InitiatorPet = new PetDto { Id = "pet-1", Name = "Rex" },
                TargetPet = new PetDto { Id = "pet-2", Name = "Milo" },
                MatchPurpose = PetPurpose.Friendship,
                Status = MatchStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

        var response = await client.PostAsJsonAsync("/api/matches/super-sniff", new
        {
            InitiatorPetId = "pet-1",
            TargetPetId = "pet-2",
            MatchPurpose = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        _factory.MatchServiceMock.Verify(s => s.CreateMatchAsync("user-1", It.IsAny<CreateMatchDto>()), Times.Once);
        _factory.UsageServiceMock.Verify(s => s.RecordUsage("user-1", UsageType.SuperSniff), Times.Once);
    }

    private sealed record LimitResponse
    {
        public string? Error { get; init; }
        public int? Limit { get; init; }
        public int? Used { get; init; }
    }
}

public class SuperSniffWebApplicationFactory : CustomWebApplicationFactory
{
    public Mock<IUsageService> UsageServiceMock { get; } = new();
    public Mock<IEntitlementService> EntitlementServiceMock { get; } = new();
    public Mock<IMatchService> MatchServiceMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            RemoveService<IUsageService>(services);
            RemoveService<IEntitlementService>(services);
            RemoveService<IMatchService>(services);

            EntitlementServiceMock.Reset();
            EntitlementServiceMock.Setup(s => s.GetEntitlementAsync(It.IsAny<string>()))
                .ReturnsAsync(new EntitlementSnapshotDto
                {
                    BillingPlan = SubscriptionPlan.Free,
                    EffectivePlan = SubscriptionPlan.Free,
                    EffectiveStatus = EntitlementStatus.Active,
                    SubscriptionStatus = SubscriptionStatus.Active,
                    Limits = PlanLimits.GetLimits(SubscriptionPlan.Free),
                    TotalPets = 1,
                    ActivePets = 1,
                    LockedPets = 0,
                    IsOverPetLimit = false,
                    PetStates = Array.Empty<PetEntitlementStateDto>(),
                    LockedPetIds = Array.Empty<string>()
                });

            services.AddSingleton(UsageServiceMock.Object);
            services.AddSingleton(EntitlementServiceMock.Object);
            services.AddSingleton(MatchServiceMock.Object);
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }
    }
}