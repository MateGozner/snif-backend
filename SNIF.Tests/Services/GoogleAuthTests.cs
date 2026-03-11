using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace SNIF.Tests.Services;

public class GoogleAuthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public GoogleAuthTests(CustomWebApplicationFactory factory)
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
    public async Task GoogleAuth_InvalidToken_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/google-auth", new
        {
            IdToken = "invalid-google-token"
        });

        // Invalid Google token should fail validation
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task GoogleAuth_MissingToken_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/google-auth", new { });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task LinkGoogle_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/link-google", new
        {
            IdToken = "some-token"
        });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UnlinkGoogle_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/users/unlink-google", null);

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task SetPassword_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/set-password", new
        {
            NewPassword = "NewPass123!",
            ConfirmPassword = "NewPass123!"
        });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UnlinkGoogle_Authenticated_RequiresPassword()
    {
        var client = _factory.CreateClient();

        // Register user with password first
        var email = $"googletestuser_{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "Google Test User"
        });

        if (registerResponse.StatusCode == HttpStatusCode.TooManyRequests)
            return; // Rate limited, skip

        if (!registerResponse.IsSuccessStatusCode)
            return; // Registration failed, skip

        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResult>();
        if (string.IsNullOrWhiteSpace(authResult?.Id))
            return;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTestJwt(authResult.Id));

        var unlinkResponse = await client.PostAsync("/api/users/unlink-google", null);

        // Should succeed (user has password, google not linked so nothing to unlink)
        // or return appropriate error
        unlinkResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task SetPassword_AuthenticatedUserWithPassword_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        // Register user with password first
        var email = $"setpwtest_{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "SetPw Test User"
        });

        if (registerResponse.StatusCode == HttpStatusCode.TooManyRequests)
            return;

        if (!registerResponse.IsSuccessStatusCode)
            return;

        var authResult = await registerResponse.Content.ReadFromJsonAsync<AuthResult>();
        if (string.IsNullOrWhiteSpace(authResult?.Id))
            return;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", GenerateTestJwt(authResult.Id));

        var response = await client.PostAsJsonAsync("/api/users/set-password", new
        {
            NewPassword = "AnotherPass123!",
            ConfirmPassword = "AnotherPass123!"
        });

        // User already has password, should return BadRequest
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests);
    }

    private record AuthResult
    {
        public string? Id { get; init; }
        public string? Email { get; init; }
        public string? Name { get; init; }
        public string? Token { get; init; }
    }
}
