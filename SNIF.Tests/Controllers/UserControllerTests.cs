using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SNIF.API.Controllers;
using SNIF.Core.DTOs;
using SNIF.Core.Exceptions;
using SNIF.Core.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace SNIF.Tests.Controllers;

public class UserControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public UserControllerTests(CustomWebApplicationFactory factory)
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
    public async Task CreateUser_Endpoint_Exists()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            Email = "test@example.com",
            Password = "TestPass123!",
            Name = "Test User"
        });

        // Should not be 404 (endpoint exists), could be 400/200 depending on Identity setup
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_Endpoint_Exists()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/users/token", new
        {
            Email = "test@example.com",
            Password = "wrong"
        });

        // Should not be 404
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUser_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/nonexistent-user-id");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUserPicture_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/users/nonexistent-user-id/picture");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UpdateUser_WithoutAuth_Returns401OrRateLimited()
    {
        var client = _factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/users/user1", new { Name = "New Name" });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UpdateUser_DifferentUser_Returns403OrRateLimited()
    {
        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // user1 trying to update user2's profile
        var response = await client.PutAsJsonAsync("/api/users/user2", new { Name = "Hacker" });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Logout_WithAuth_Returns204()
    {
        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.DeleteAsync("/api/users/token");

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NoContent, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task UpdatePreferences_DifferentUser_Returns403OrRateLimited()
    {
        var client = _factory.CreateClient();
        var token = GenerateTestJwt("user1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PutAsJsonAsync("/api/users/user2/preferences", new { SearchRadius = 100 });

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Forbidden, HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task CreateToken_WhenActivationPending_ReturnsStructured403()
    {
        var pendingResponse = new AuthResponseDto
        {
            Id = "user-1",
            Email = "pending@example.com",
            Name = "PendingUser",
            EmailConfirmed = false,
            AuthStatus = "PendingActivation",
            RequiresEmailConfirmation = true,
            CanResendConfirmation = true,
            Message = "Email confirmation is required before you can sign in."
        };

        var userService = new Mock<IUserService>();
        userService.Setup(s => s.LoginUserAsync(It.IsAny<LoginDto>()))
            .ThrowsAsync(new PendingActivationException(pendingResponse));

        var controller = new UserController(userService.Object, Mock.Of<IWebHostEnvironment>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.CreateToken(new LoginDto
        {
            Email = "pending@example.com",
            Password = "ValidPass123!"
        });

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);

        var body = objectResult.Value.Should().BeOfType<AuthResponseDto>().Subject;
        body.Token.Should().BeNull();
        body.AuthStatus.Should().Be("PendingActivation");
        body.RequiresEmailConfirmation.Should().BeTrue();
        body.CanResendConfirmation.Should().BeTrue();
    }
}
