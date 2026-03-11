using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using SNIF.Core.Interfaces;

namespace SNIF.Infrastructure.Services
{
    public class GoogleAuthService : IGoogleAuthService
    {
        private readonly IReadOnlyList<string> _validClientIds;

        public GoogleAuthService(IConfiguration config)
        {
            var clientIds = new List<string>();

            var webClientId = config["Google:ClientId"];
            if (!string.IsNullOrEmpty(webClientId)) clientIds.Add(webClientId);

            var iosClientId = config["Google:ClientIdIos"];
            if (!string.IsNullOrEmpty(iosClientId)) clientIds.Add(iosClientId);

            var androidClientId = config["Google:ClientIdAndroid"];
            if (!string.IsNullOrEmpty(androidClientId)) clientIds.Add(androidClientId);

            _validClientIds = clientIds.AsReadOnly();
        }

        public async Task<GoogleUserInfo> ValidateGoogleTokenAsync(string idToken)
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
    }
}
