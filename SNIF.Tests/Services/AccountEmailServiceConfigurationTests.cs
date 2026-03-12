using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SNIF.API.Extensions;
using SNIF.Application.Services;
using SNIF.Core.Interfaces;

namespace SNIF.Tests.Services;

public class AccountEmailServiceConfigurationTests
{
    [Fact]
    public void AddApplicationServices_DefaultProvider_UsesLoggingFallback()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>());
        var services = CreateServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var emailService = serviceProvider.GetRequiredService<IAccountEmailService>();

        emailService.Should().BeOfType<LoggingAccountEmailService>();
    }

    [Fact]
    public void AddApplicationServices_AzureProviderWithoutSenderAddress_UsesLoggingFallback()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "AzureCommunication",
            ["Email:ConnectionString"] = "endpoint=https://example.communication.azure.com/;accesskey=ZmFrZQ=="
        });
        var services = CreateServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var emailService = serviceProvider.GetRequiredService<IAccountEmailService>();

        emailService.Should().BeOfType<LoggingAccountEmailService>();
    }

    [Fact]
    public void AddApplicationServices_AzureProviderWithRequiredSettings_UsesAzureCommunicationService()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "AzureCommunication",
            ["Email:ConnectionString"] = "endpoint=https://example.communication.azure.com/;accesskey=ZmFrZQ==",
            ["Email:SenderAddress"] = "no-reply@example.azurecomm.net"
        });
        var services = CreateServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var emailService = serviceProvider.GetRequiredService<IAccountEmailService>();

        emailService.Should().BeOfType<AzureCommunicationAccountEmailService>();
    }

    [Fact]
    public void AddApplicationServices_AzureCommunicationServicesProviderWithRequiredSettings_UsesAzureCommunicationService()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Email:Provider"] = "AzureCommunicationServices",
            ["Email:ConnectionString"] = "endpoint=https://example.communication.azure.com/;accesskey=ZmFrZQ==",
            ["Email:SenderAddress"] = "no-reply@example.azurecomm.net"
        });
        var services = CreateServices(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var emailService = serviceProvider.GetRequiredService<IAccountEmailService>();

        emailService.Should().BeOfType<AzureCommunicationAccountEmailService>();
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private static ServiceCollection CreateServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddApplicationServices(configuration);
        return services;
    }
}