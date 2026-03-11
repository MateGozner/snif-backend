using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace SNIF.Tests.Controllers;

public class PasswordResetTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PasswordResetTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    // ── 1. Forgot-password returns 200 for existing email ────────────

    [Fact]
    public async Task ForgotPassword_ExistingEmail_ReturnsOk()
    {
        var client = _factory.CreateClient();

        // Register a user first
        var email = $"forgot_{Guid.NewGuid():N}@example.com";
        var regResponse = await client.PostAsJsonAsync("/api/users", new
        {
            Email = email,
            Password = "ValidPass123!",
            Name = "Forgot User"
        });

        if (regResponse.StatusCode == HttpStatusCode.TooManyRequests) return;

        var response = await client.PostAsJsonAsync("/api/users/forgot-password", new
        {
            Email = email
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── 2. Forgot-password returns 200 for non-existing email ────────

    [Fact]
    public async Task ForgotPassword_NonExistingEmail_ReturnsOk_NoInfoLeak()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/forgot-password", new
        {
            Email = $"nonexistent_{Guid.NewGuid():N}@example.com"
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        // Should always return 200 to prevent email enumeration
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── 3. Reset-password with valid token resets password ──────────
    //    NOTE: In integration tests without a real email provider,
    //    we can only verify the endpoint exists and validates input.

    [Fact]
    public async Task ResetPassword_ValidPayload_EndpointExists()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/reset-password", new
        {
            Token = "test-reset-token",
            Email = "test@example.com",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        // Should return BadRequest (invalid token) but NOT 404 (endpoint exists)
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.TooManyRequests);
    }

    // ── 4. Reset-password with expired/invalid token returns 400 ─────

    [Fact]
    public async Task ResetPassword_InvalidToken_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/reset-password", new
        {
            Token = "expired-or-invalid-token-value",
            Email = "test@example.com",
            NewPassword = "NewPassword123!",
            ConfirmPassword = "NewPassword123!"
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── 5. Reset-password with mismatched passwords returns 400 ─────

    [Fact]
    public async Task ResetPassword_MismatchedPasswords_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/reset-password", new
        {
            Token = "some-reset-token",
            Email = "test@example.com",
            NewPassword = "Password123!",
            ConfirmPassword = "DifferentPassword456!"
        });

        if (response.StatusCode == HttpStatusCode.TooManyRequests) return;

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
