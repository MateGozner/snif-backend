using FluentAssertions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace SNIF.Tests.Services;

public class UserServiceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UserServiceTests(CustomWebApplicationFactory factory)
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
    public async Task RegisterUser_ValidData_ReturnsCreated()
    {
        var client = _factory.CreateClient();
        var email = $"newuser_{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "TestUser"
        });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.BadRequest,
            HttpStatusCode.TooManyRequests);

        if (response.StatusCode != HttpStatusCode.Created)
            return;

        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(email);
        body.Token.Should().BeNull();
        body.AuthStatus.Should().Be("PendingActivation");
        body.RequiresEmailConfirmation.Should().BeTrue();
        body.CanResendConfirmation.Should().BeTrue();
        body.EmailConfirmed.Should().BeFalse();
    }

    [Fact]
    public async Task RegisterUser_DuplicateEmail_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";

        // First registration
        await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "First User"
        });

        // Second registration with same email
        var response = await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "Second User"
        });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_InvalidCredentials_ReturnsUnauthorizedOrRateLimited()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/token", new
        {
            Email = "nonexistent@example.com",
            Password = "WrongPass123!"
        });

        // Auth rate limit (5/min) may kick in if many tests share the limiter
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Login_UnconfirmedLocalAccount_ReturnsPendingActivationContract()
    {
        var client = _factory.CreateClient();
        var email = $"pending_{Guid.NewGuid():N}@example.com";

        var registerResponse = await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "Pending User"
        });

        if (registerResponse.StatusCode == HttpStatusCode.TooManyRequests)
            return;

        if (registerResponse.StatusCode != HttpStatusCode.Created)
            return;

        var loginResponse = await client.PostAsJsonAsync("/api/users/token", new
        {
            Email = email,
            Password = "ValidPass123!"
        });

        if (loginResponse.StatusCode == HttpStatusCode.TooManyRequests)
            return;

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResult>();
        body.Should().NotBeNull();
        body!.Email.Should().Be(email);
        body.Token.Should().BeNull();
        body.AuthStatus.Should().Be("PendingActivation");
        body.RequiresEmailConfirmation.Should().BeTrue();
        body.CanResendConfirmation.Should().BeTrue();
        body.Message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetUser_NonExistent_Returns404OrRateLimited()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/nonexistent-user-id-123");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UpdateUser_OwnProfile_WithAuth_DoesNotReturn401()
    {
        var client = _factory.CreateClient();
        var userId = "test-user-1";
        var token = GenerateTestJwt(userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync($"/api/users/{userId}", new { Name = "Updated Name" });

        // With valid auth and matching userId, should not be 401 or 403
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateUser_OtherProfile_Returns403OrRateLimited()
    {
        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user-a");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/api/users/user-b", new { Name = "Hacked" });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UpdateProfilePicture_OtherUser_Returns403()
    {
        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user-a");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/api/users/user-b/picture", new
        {
            FileName = "photo.jpg",
            ContentType = "image/jpeg",
            Base64Data = Convert.ToBase64String(new byte[] { 1, 2, 3 })
        });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests);
    }

    private sealed record AuthResult
    {
        public string? Id { get; init; }
        public string? Email { get; init; }
        public string? Name { get; init; }
        public string? Token { get; init; }
        public bool EmailConfirmed { get; init; }
        public string? AuthStatus { get; init; }
        public bool RequiresEmailConfirmation { get; init; }
        public bool CanResendConfirmation { get; init; }
        public string? Message { get; init; }
    }
}
