namespace SNIF.Core.Interfaces
{
    public interface IGoogleAuthService
    {
        Task<GoogleUserInfo> ValidateGoogleTokenAsync(string idToken);
    }

    public record GoogleUserInfo(
        string SubjectId,
        string Email,
        string Name,
        string? PictureUrl
    );
}
