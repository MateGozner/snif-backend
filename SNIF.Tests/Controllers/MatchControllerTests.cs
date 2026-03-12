using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace SNIF.Tests.Controllers;

public class MatchControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public MatchControllerTests(CustomWebApplicationFactory factory)
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
    public async Task GetMatches_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/matches?petId=pet1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMatches_WithAuth_NoPetId_Returns400()
    {
        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/matches");

        // No petId parameter → 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateMatch_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/matches", new
        {
            InitiatorPetId = "pet1",
            TargetPetId = "pet2",
            MatchPurpose = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteMatch_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/matches/match1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPotentialMatches_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/matches/potential?petId=pet1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPendingMatches_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/matches/pet/pet1/pending");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────
    // IDOR / Ownership Tests
    // ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetMatches_ForOtherUsersPet_Returns403()
    {
        // Seed a pet owned by user-A
        var petId = $"idor-pet-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SNIFContext>();
            context.Users.Add(new User
            {
                Id = "user-A",
                UserName = "user-A",
                Name = "User A",
                Email = "userA@test.com",
                CreatedAt = DateTime.UtcNow,
                EmailConfirmed = true
            });
            context.Pets.Add(new Pet
            {
                Id = petId,
                Name = "Buddy",
                Species = "Dog",
                Breed = "Labrador",
                Age = 3,
                Gender = Gender.Male,
                OwnerId = "user-A",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // Authenticate as user-B and try to access user-A's pet matches
        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user-B");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/matches?petId={petId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPendingMatches_ForOtherUsersPet_Returns403()
    {
        var petId = $"idor-pending-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SNIFContext>();
            if (!await context.Users.AnyAsync(u => u.Id == "user-owner"))
            {
                context.Users.Add(new User
                {
                    Id = "user-owner",
                    UserName = "user-owner",
                    Name = "Owner",
                    Email = "owner@test.com",
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true
                });
            }
            context.Pets.Add(new Pet
            {
                Id = petId,
                Name = "Max",
                Species = "Dog",
                Breed = "Poodle",
                Age = 5,
                Gender = Gender.Female,
                OwnerId = "user-owner",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user-attacker");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/matches/pet/{petId}/pending");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateMatch_WithOtherUsersPet_Returns403()
    {
        var petId = $"idor-create-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SNIFContext>();
            if (!await context.Users.AnyAsync(u => u.Id == "user-victim"))
            {
                context.Users.Add(new User
                {
                    Id = "user-victim",
                    UserName = "user-victim",
                    Name = "Victim",
                    Email = "victim@test.com",
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true
                });
            }
            context.Pets.Add(new Pet
            {
                Id = petId,
                Name = "Rex",
                Species = "Dog",
                Breed = "Shepherd",
                Age = 4,
                Gender = Gender.Male,
                OwnerId = "user-victim",
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user-attacker2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/matches", new
        {
            InitiatorPetId = petId,
            TargetPetId = "some-target-pet",
            MatchPurpose = 0
        });

        // Should be 403 (can't create match from someone else's pet)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMatches_ForOwnPet_Returns200()
    {
        var petId = $"own-pet-{Guid.NewGuid():N}";
        const string ownerId = "user-own-pet-owner";
        using (var scope = _factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<SNIFContext>();
            if (!await context.Users.AnyAsync(u => u.Id == ownerId))
            {
                context.Users.Add(new User
                {
                    Id = ownerId,
                    UserName = ownerId,
                    Name = "Owner",
                    Email = "ownpet@test.com",
                    CreatedAt = DateTime.UtcNow,
                    EmailConfirmed = true
                });
            }
            context.Pets.Add(new Pet
            {
                Id = petId,
                Name = "Luna",
                Species = "Dog",
                Breed = "Golden",
                Age = 2,
                Gender = Gender.Female,
                OwnerId = ownerId,
                CreatedAt = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var token = GenerateTestJwt(ownerId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync($"/api/matches?petId={petId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
