using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SNIF.API.Extensions;
using SNIF.Core.Configuration;
using SNIF.Infrastructure.Data;
using SNIF.Infrastructure.Services;

namespace SNIF.Tests;

public class SecurityConfigurationTests
{
    private const string ValidJwtKey = "this-is-a-test-jwt-key-with-at-least-sixty-four-bytes-of-entropy-12345";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dev-only-jwt-key-change-before-deploying-0123456789abcdef0123456789abcdef0123456789abcdef")]
    [InlineData("short-key")]
    public void JwtKeyValidator_InvalidKeys_Throw(string? configuredKey)
    {
        var act = () => JwtKeyValidator.GetValidatedKeyBytes(configuredKey);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void TokenService_InvalidPlaceholderKey_Throws()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "dev-only-jwt-key-change-before-deploying-0123456789abcdef0123456789abcdef0123456789abcdef",
                ["Jwt:Issuer"] = "http://localhost:3000",
                ["Jwt:Audience"] = "http://localhost:3000"
            })
            .Build();

        var act = () => new TokenService(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*placeholder*");
    }

    [Fact]
    public void AddIdentityServices_InvalidJwtKey_ThrowsDuringStartupRegistration()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SNIFContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "short-key",
                ["Jwt:Issuer"] = "http://localhost:3000",
                ["Jwt:Audience"] = "http://localhost:3000"
            })
            .Build();

        var act = () => services.AddIdentityServices(configuration);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT signing key*");
    }

    [Fact]
    public void AddIdentityServices_ValidJwtKey_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SNIFContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = ValidJwtKey,
                ["Jwt:Issuer"] = "http://localhost:3000",
                ["Jwt:Audience"] = "http://localhost:3000"
            })
            .Build();

        var act = () => services.AddIdentityServices(configuration);

        act.Should().NotThrow();
    }

    [Fact]
    public void GoogleClientIdValidator_MissingClientIds_ThrowsWhenRequired()
    {
        var act = () => GoogleClientIdValidator.GetValidatedClientIds(
            webClientId: null,
            iosClientId: null,
            androidClientId: null,
            requireAtLeastOneClientId: true);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Google OAuth client IDs are missing*");
    }

    [Fact]
    public void GoogleClientIdValidator_ConfiguredWebClientId_ReturnsTrimmedValue()
    {
        var clientIds = GoogleClientIdValidator.GetValidatedClientIds(
            webClientId: "  test-web-client-id  ",
            iosClientId: null,
            androidClientId: null,
            requireAtLeastOneClientId: true);

        clientIds.Should().ContainSingle()
            .Which.Should().Be("test-web-client-id");
    }
}