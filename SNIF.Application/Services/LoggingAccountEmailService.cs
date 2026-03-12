using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;

namespace SNIF.Application.Services
{
    public class LoggingAccountEmailService : IAccountEmailService
    {
        private readonly ILogger<LoggingAccountEmailService> _logger;
        private readonly IConfiguration _configuration;

        public LoggingAccountEmailService(ILogger<LoggingAccountEmailService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public Task SendPasswordResetAsync(User user, string token)
        {
            var content = AccountEmailContentFactory.CreatePasswordReset(user, token, _configuration["App:PublicBaseUrl"]);
            LogAction(content.Action, user.Email, content.Link);
            return Task.CompletedTask;
        }

        public Task SendEmailConfirmationAsync(User user, string token)
        {
            var content = AccountEmailContentFactory.CreateEmailConfirmation(user, token, _configuration["App:PublicBaseUrl"]);
            LogAction(content.Action, user.Email, content.Link);
            return Task.CompletedTask;
        }

        private void LogAction(string action, string? email, string? link)
        {
            var recipientAddressConfigured = !string.IsNullOrWhiteSpace(email);
            var publicBaseUrlConfigured = !string.IsNullOrWhiteSpace(_configuration["App:PublicBaseUrl"]);
            var actionLinkGenerated = !string.IsNullOrWhiteSpace(link);

            _logger.LogInformation(
                "Issued {Action} email flow using logging fallback. Delivery provider is not configured. RecipientAddressConfigured: {RecipientAddressConfigured}. PublicBaseUrlConfigured: {PublicBaseUrlConfigured}. ActionLinkGenerated: {ActionLinkGenerated}.",
                action,
                recipientAddressConfigured,
                publicBaseUrlConfigured,
                actionLinkGenerated);
        }
    }
}