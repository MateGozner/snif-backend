using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SNIF.API.HealthChecks;

public class LemonSqueezyHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public LemonSqueezyHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["LemonSqueezy:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            return Task.FromResult(HealthCheckResult.Degraded("LemonSqueezy API key not configured"));

        var signingSecret = _configuration["LemonSqueezy:SigningSecret"];
        if (string.IsNullOrEmpty(signingSecret))
            return Task.FromResult(HealthCheckResult.Degraded("LemonSqueezy signing secret not configured"));

        return Task.FromResult(HealthCheckResult.Healthy("LemonSqueezy configured"));
    }
}
