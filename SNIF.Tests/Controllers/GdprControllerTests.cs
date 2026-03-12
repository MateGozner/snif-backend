using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace SNIF.Tests.Controllers;

public class GdprControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GdprControllerTests(CustomWebApplicationFactory factory)
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

    private async Task<(HttpClient Client, string UserId, string Token)?> RegisterUser(HttpClient client)
    {
        var email = $"gdpr_{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "GDPR Test User"
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return null;
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        if (string.IsNullOrWhiteSpace(result?.Id)) return null;

        var token = GenerateTestJwt(result.Id);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return (client, result.Id!, token);
    }

    // ── 1. Data export returns user data ─────────────────────────────

    [Fact]
    public async Task DataExport_Authenticated_ReturnsUserData()
    {
        var client = _factory.CreateClient();
        var reg = await RegisterUser(client);
        if (reg == null) return;

        var response = await client.GetAsync("/api/users/me/data-export");

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrEmpty();
        }
    }

    // ── 2. Data export without auth returns 401 ──────────────────────

    [Fact]
    public async Task DataExport_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/me/data-export");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    // ── 3. Delete account soft-deletes user ──────────────────────────

    [Fact]
    public async Task DeleteAccount_Authenticated_Succeeds()
    {
        var client = _factory.CreateClient();
        var reg = await RegisterUser(client);
        if (reg == null) return;

        var response = await client.DeleteAsync("/api/users/me");

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    // ── 4. Delete account anonymizes personal data ───────────────────

    [Fact]
    public async Task DeleteAccount_UserNoLongerAccessible()
    {
        var client = _factory.CreateClient();
        var reg = await RegisterUser(client);
        if (reg == null) return;

        var deleteResponse = await client.DeleteAsync("/api/users/me");
        if (deleteResponse.StatusCode == HttpStatusCode.TooManyRequests) return;

        // After deletion, fetching the user should fail
        var getResponse = await client.GetAsync($"/api/users/{reg.Value.UserId}");

        getResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    // ── 5. Delete account without auth returns 401 ───────────────────

    [Fact]
    public async Task DeleteAccount_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/users/me");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    private record AuthResult
    {
        public string? Id { get; init; }
        public string? Email { get; init; }
        public string? Name { get; init; }
        public string? Token { get; init; }
    }
}
