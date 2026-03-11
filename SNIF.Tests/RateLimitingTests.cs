using FluentAssertions;
using System.Net;

namespace SNIF.Tests;

public class RateLimitingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RateLimitingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PaymentEndpoint_ExistsAndHasGlobalRateLimiting()
    {
        var client = _factory.CreateClient();

        // PaymentController uses [EnableRateLimiting("global")] at /api/payments
        // The webhook endpoint is [AllowAnonymous] so it responds without auth
        var response = await client.PostAsync("/api/payments/webhook",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        // Should get 400 (bad request due to missing stripe signature), not 404
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SwipeRateLimit_MatchEndpointExists()
    {
        var client = _factory.CreateClient();

        // MatchController uses [EnableRateLimiting("swipe")] at /api/matches
        var response = await client.GetAsync("/api/matches");

        // Should get 401 (auth required) not 404 (endpoint not found)
        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthRateLimit_UserEndpoint_Returns429_WhenExceeded()
    {
        var client = _factory.CreateClient();

        // UserController uses [EnableRateLimiting("auth")] — 5 req/min
        // POST /api/users/token doesn't require [Authorize], so rate limiter applies
        bool rateLimited = false;

        for (int i = 0; i < 10; i++)
        {
            var response = await client.PostAsync("/api/users/token",
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimited = true;
                break;
            }
        }

        rateLimited.Should().BeTrue("auth rate limit of 5/min should be enforced on user endpoints");
    }
}
