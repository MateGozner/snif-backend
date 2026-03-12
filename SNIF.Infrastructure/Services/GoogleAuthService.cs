using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SNIF.Core.Configuration;
using SNIF.Core.Interfaces;

namespace SNIF.Infrastructure.Services
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly IReadOnlyList<string> _validClientIds;
        private readonly IReadOnlyList<string> _configuredClientTypes;
        private readonly ILogger<GoogleAuthService> _logger;

        public GoogleAuthService(IConfiguration config, ILogger<GoogleAuthService> logger)
        {
            _logger = logger;

            var webClientId = config["Google:ClientId"];
            var iosClientId = config["Google:ClientIdIos"];
            var androidClientId = config["Google:ClientIdAndroid"];

            _validClientIds = GoogleClientIdValidator.GetValidatedClientIds(
                webClientId,
                iosClientId,
                androidClientId,
                requireAtLeastOneClientId: false);

            _configuredClientTypes = new[]
            {
                (Name: "web", Value: webClientId),
                (Name: "ios", Value: iosClientId),
                (Name: "android", Value: androidClientId)
            }
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .Select(item => item.Name)
            .ToArray();
        }

        public async Task<GoogleUserInfo> ValidateGoogleTokenAsync(string idToken)
        {
            try
            {
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = _validClientIds
                };

                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

                return new GoogleUserInfo(
                    SubjectId: payload.Subject,
                    Email: payload.Email,
                    Name: payload.Name,
                    PictureUrl: payload.Picture
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Google token validation failed. ConfiguredClientTypes: {ConfiguredClientTypes}. AudienceCount: {AudienceCount}. TokenLength: {TokenLength}.",
                    _configuredClientTypes,
                    _validClientIds.Count,
                    idToken?.Length ?? 0);

                throw;
            }
        }
    }
}
