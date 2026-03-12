using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using SNIF.Application.Services;
using SNIF.Core.Entities;

namespace SNIF.Tests.Services;

public class LoggingAccountEmailServiceTests
{
    [Theory]
    [InlineData("SendEmailConfirmationAsync", "confirm-email")]
    [InlineData("SendPasswordResetAsync", "reset-password")]
    public async Task FallbackLogging_DoesNotLogTokenOrFullActionUrl(string operation, string route)
    {
        const string token = "redeemable-token-123+/=";
        const string publicBaseUrl = "https://snif.example.com";

        var expectedUrl = $"{publicBaseUrl}/{route}?email={Uri.EscapeDataString("person@example.com")}&token={Uri.EscapeDataString(token)}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:PublicBaseUrl"] = publicBaseUrl
            })
            .Build();
        var loggerMock = new Mock<ILogger<LoggingAccountEmailService>>();
        var service = new LoggingAccountEmailService(loggerMock.Object, configuration);
        var user = new User
        {
            Email = "person@example.com",
            Name = "Person"
        };

        if (operation == "SendEmailConfirmationAsync")
        {
            await service.SendEmailConfirmationAsync(user, token);
        }
        else
        {
            await service.SendPasswordResetAsync(user, token);
        }

        var logEntry = loggerMock.Invocations.Should().ContainSingle().Subject;
        var message = logEntry.Arguments[2]?.ToString();
        var values = logEntry.Arguments[2] as IEnumerable<KeyValuePair<string, object?>>;

        message.Should().NotBeNullOrWhiteSpace();
        message.Should().NotContain(token);
        message.Should().NotContain(expectedUrl);
        message.Should().NotContain(user.Email);
        message.Should().Contain("RecipientAddressConfigured: True");
        message.Should().Contain("PublicBaseUrlConfigured: True");
        message.Should().Contain("ActionLinkGenerated: True");

        values.Should().NotBeNull();
        values!.Select(pair => pair.Value?.ToString())
            .Should()
            .NotContain(value => value == token || value == expectedUrl || value == user.Email);
    }
}