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
        Task<UserDto> UpdateUserPersonalInfo(string userId, UpdateUserPersonalInfoDto updateUserPersonalInfoDto);
        Task<UserDto> UpdateUserPreferences(string userId, UpdatePreferencesDto preferencesDto);
    }
}
