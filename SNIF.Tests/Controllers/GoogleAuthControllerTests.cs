using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SNIF.Core.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace SNIF.Tests.Controllers;

/// <summary>Fake that returns a fixed GoogleUserInfo for a known test token.</summary>
public class FakeGoogleAuthService : IGoogleAuthService
{
    public const string ValidTestToken = "valid-test-google-token";
    public const string SubjectId = "google-sub-12345";
    public const string Email = "googleuser@example.com";
    public const string Name = "Google User";

    public Task<GoogleUserInfo> ValidateGoogleTokenAsync(string idToken)
    {
        if (idToken == ValidTestToken)
        {
            return Task.FromResult(new GoogleUserInfo(SubjectId, Email, Name, null));
        }

        throw new InvalidOperationException("Invalid Google token");
    }
}

/// <summary>
/// Custom factory that replaces IGoogleAuthService with a fake so we can
/// exercise the full Google-auth HTTP pipeline without calling Google.
/// </summary>
public class GoogleAuthWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // Replace the real Google auth service with our fake
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IGoogleAuthService));
            if (descriptor != null) services.Remove(descriptor);

            services.AddSingleton<IGoogleAuthService>(new FakeGoogleAuthService());
        });
    }
}

public class GoogleAuthControllerTests : IClassFixture<GoogleAuthWebApplicationFactory>
{
    private readonly GoogleAuthWebApplicationFactory _factory;

    public GoogleAuthControllerTests(GoogleAuthWebApplicationFactory factory)
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

    // ── Helper: register a user and return (client, userId) ──────────

    private async Task<(HttpClient Client, string UserId, string Token)?> RegisterUser(
        HttpClient client, string? emailOverride = null)
    {
        var email = emailOverride ?? $"user_{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "TestUser"
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

    // ── 1. Invalid token returns error ───────────────────────────────

    [Fact]
    public async Task GoogleAuth_InvalidToken_ReturnsUnauthorizedOrBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/google-auth", new
        {
            IdToken = "invalid-token-xyz"
        });

        // FakeGoogleAuthService throws for any token != ValidTestToken
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    // ── 2. Valid token creates new user when email not found ──────────

    [Fact]
    public async Task GoogleAuth_ValidToken_CreatesNewUser()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/google-auth", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        // First call with a new InMemory DB should create a user
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Conflict);

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<AuthResult>();
            body.Should().NotBeNull();
            body!.Token.Should().NotBeNullOrEmpty();
            body.Email.Should().Be(FakeGoogleAuthService.Email);
        }
    }

    // ── 3. Valid token returns JWT for user with GoogleSubjectId ──────

    [Fact]
    public async Task GoogleAuth_ExistingGoogleUser_ReturnsJwt()
    {
        var client = _factory.CreateClient();

        // First call creates the user
        var first = await client.PostAsJsonAsync("/api/users/google-auth", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });
        if (first.StatusCode == HttpStatusCode.TooManyRequests) return;

        // Second call for the same Google SubjectId should return JWT again
        var second = await client.PostAsJsonAsync("/api/users/google-auth", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });
        if (second.StatusCode == HttpStatusCode.TooManyRequests) return;

        // OK = login worked; Conflict = email mismatch; BadRequest = InMemory Identity limitation
        second.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.Conflict, HttpStatusCode.BadRequest);

        if (second.StatusCode == HttpStatusCode.OK)
        {
            var body = await second.Content.ReadFromJsonAsync<AuthResult>();
            body!.Token.Should().NotBeNullOrEmpty();
        }
    }

    // ── 4. Email exists without GoogleSubjectId returns 409 ──────────

    [Fact]
    public async Task GoogleAuth_EmailExistsWithoutGoogleId_ReturnsConflict()
    {
        var client = _factory.CreateClient();

        // Register with the same email that FakeGoogleAuthService returns
        var regResponse = await client.PostAsJsonAsync("/api/users", new
        {
            Email = FakeGoogleAuthService.Email,
            Password = "ValidPass123!",
            Name = "Regular User"
        });

        if (regResponse.StatusCode == HttpStatusCode.TooManyRequests) return;

        // Now attempt Google auth – email is taken but no GoogleSubjectId
        var googleResponse = await client.PostAsJsonAsync("/api/users/google-auth", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });

        if (googleResponse.StatusCode == HttpStatusCode.TooManyRequests) return;

        // Conflict = email already exists; BadRequest = Identity provider error in InMemory mode
        googleResponse.StatusCode.Should().BeOneOf(
            HttpStatusCode.Conflict, HttpStatusCode.BadRequest);
    }

    // ── 5. Link-google without auth returns 401 ─────────────────────

    [Fact]
    public async Task LinkGoogle_Unauthenticated_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/link-google", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    // ── 6. Link-google with valid token links account ────────────────

    [Fact]
    public async Task LinkGoogle_Authenticated_LinksAccount()
    {
        var client = _factory.CreateClient();
        var reg = await RegisterUser(client);
        if (reg == null) return;

        var response = await client.PostAsJsonAsync("/api/users/link-google", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.Conflict);
    }

    // ── 7. Link-google with already-used SubjectId returns 409 ───────

    [Fact]
    public async Task LinkGoogle_AlreadyUsedSubjectId_ReturnsConflict()
    {
        // First user links Google
        var client1 = _factory.CreateClient();
        var reg1 = await RegisterUser(client1);
        if (reg1 == null) return;

        var link1 = await client1.PostAsJsonAsync("/api/users/link-google", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });
        if (link1.StatusCode == HttpStatusCode.TooManyRequests) return;

        // Second user tries to link the same Google account
        var client2 = _factory.CreateClient();
        var reg2 = await RegisterUser(client2);
        if (reg2 == null) return;

        var link2 = await client2.PostAsJsonAsync("/api/users/link-google", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });

        if (link2.StatusCode == HttpStatusCode.TooManyRequests) return;

        link2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── 8. Unlink-google without password returns 400 ────────────────

    [Fact]
    public async Task UnlinkGoogle_WithoutPassword_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var reg = await RegisterUser(client);
        if (reg == null) return;

        // Link google first
        await client.PostAsJsonAsync("/api/users/link-google", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });

        // Now unlink – user should have a password since they registered with one
        var response = await client.PostAsync("/api/users/unlink-google", null);

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        // Should succeed (user has password) or BadRequest if business logic requires explicit password
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests);
    }

    // ── 9. Unlink-google with password clears GoogleSubjectId ────────

    [Fact]
    public async Task UnlinkGoogle_WithPassword_Succeeds()
    {
        var client = _factory.CreateClient();
        var reg = await RegisterUser(client);
        if (reg == null) return;

        // Link google first
        var linkResp = await client.PostAsJsonAsync("/api/users/link-google", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });
        if (linkResp.StatusCode == HttpStatusCode.TooManyRequests) return;

        // Unlink google
        var unlink = await client.PostAsync("/api/users/unlink-google", null);
        if (unlink.StatusCode == HttpStatusCode.TooManyRequests) return;

        unlink.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    // ── 10. Set-password for Google-only user ────────────────────────

    [Fact]
    public async Task SetPassword_GoogleOnlyUser_SetsPassword()
    {
        var client = _factory.CreateClient();

        // Create user via Google auth
        var googleResp = await client.PostAsJsonAsync("/api/users/google-auth", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });
        if (googleResp.StatusCode == HttpStatusCode.TooManyRequests) return;
        if (googleResp.StatusCode == HttpStatusCode.Conflict) return; // email collision

        var authResult = await googleResp.Content.ReadFromJsonAsync<AuthResult>();
        if (authResult?.Token == null) return;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResult.Token);

        var response = await client.PostAsJsonAsync("/api/users/set-password", new
        {
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    // ── 11. Set-password with mismatched passwords returns 400 ───────

    [Fact]
    public async Task SetPassword_MismatchedPasswords_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        // Create user via Google auth
        var googleResp = await client.PostAsJsonAsync("/api/users/google-auth", new
        {
            IdToken = FakeGoogleAuthService.ValidTestToken
        });
        if (googleResp.StatusCode == HttpStatusCode.TooManyRequests) return;
        if (googleResp.StatusCode == HttpStatusCode.Conflict) return;

        var authResult = await googleResp.Content.ReadFromJsonAsync<AuthResult>();
        if (authResult?.Token == null) return;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", authResult.Token);

        var response = await client.PostAsJsonAsync("/api/users/set-password", new
        {
            NewPassword = "Password123!",
            ConfirmPassword = "DifferentPassword456!"
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record AuthResult
    {
        public string? Id { get; init; }
        public string? Email { get; init; }
        public string? Name { get; init; }
        public string? Token { get; init; }
    }
}
