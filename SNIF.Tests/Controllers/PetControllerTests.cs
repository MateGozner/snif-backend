using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace SNIF.Tests.Controllers;

public class PetControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PetControllerTests(CustomWebApplicationFactory factory)
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
    public async Task GetPets_WithAuth_Returns200()
    {
        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/pets?userId=user1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPets_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        // GET /api/pets without auth - endpoint now requires auth
        var response = await client.GetAsync("/api/pets?userId=test");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreatePet_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/pets", new
        {
            Name = "Buddy",
            Species = "Dog",
            Breed = "Labrador",
            Age = 3
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPetById_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/pets/nonexistent-id");

        // Endpoint now requires auth at class level
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePet_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/pets/some-id");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdatePet_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/pets/some-id", new { Name = "Updated" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
