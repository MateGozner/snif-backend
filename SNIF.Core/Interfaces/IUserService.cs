using SNIF.Core.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Core.Interfaces
{
    public interface IUserService
    {
        Task<AuthResponseDto> RegisterUserAsync(CreateUserDto createUserDto);
        Task<AuthResponseDto> LoginUserAsync(LoginDto loginDto);
        Task LogoutUser();
        Task<UserDto> IsUserLoggedInByEmail(string email);
        Task<UserDto> GetUserProfileById(string userId);
        Task<UserDto> UpdateUserPersonalInfo(string userId, UpdateUserDto updateUserPersonalInfoDto);
        Task<UserDto> UpdateUserProfilePicture(string userId, UpdateProfilePictureDto pictureDto);
        Task<UserDto> UpdateUserPreferences(string userId, UpdatePreferencesDto preferencesDto);
        Task UpdateUserOnlineStatus(string userId, bool isOnline);
        Task<AuthResponseDto> ValidateAndRefreshTokenAsync(string token);
        Task<string> GetUserProfilePictureName(string id);

        // Google OAuth
        Task<AuthResponseDto> GoogleAuthAsync(GoogleAuthRequestDto request);
        Task LinkGoogleAccountAsync(string userId, LinkGoogleAccountDto request);
        Task UnlinkGoogleAccountAsync(string userId);
        Task SetPasswordAsync(string userId, SetPasswordDto request);

        // Password Reset
        Task ForgotPasswordAsync(ForgotPasswordDto dto);
        Task ResetPasswordAsync(ResetPasswordDto dto);

        // Email Confirmation
        Task ConfirmEmailAsync(ConfirmEmailDto dto);
        Task ResendConfirmationAsync(ResendConfirmationDto dto);

        // GDPR
        Task<UserDataExportDto> ExportUserDataAsync(string userId);
        Task DeleteAccountAsync(string userId);
    }
}
