using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace SNIF.Busniess.Services
{
    public class UserService : IUserService
    {
        private readonly ITokenService _tokenService;
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly SNIFContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IMapper _mapper;

        public UserService(ITokenService tokenService, SignInManager<User> signInManager, UserManager<User> userManager, SNIFContext context, IWebHostEnvironment environment, IMapper mapper)
        {
            _tokenService = tokenService;
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _environment = environment;
            _mapper = mapper;
        }
        public async Task<AuthResponseDto> RegisterUserAsync(CreateUserDto createUserDto)
        {
            if (await _userManager.FindByEmailAsync(createUserDto.Email) != null)
            {
                throw new Exception("Email already registered");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var location = _mapper.Map<Location>(createUserDto.Location);
                location.CreatedAt = DateTime.UtcNow;

                _context.Locations.Add(location);
                await _context.SaveChangesAsync();

                var user = new User
                {
                    Email = createUserDto.Email,
                    UserName = createUserDto.Name,
                    Name = createUserDto.Name,
                    Location = location,
                    CreatedAt = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(user, createUserDto.Password);
                if (!result.Succeeded)
                {
                    throw new Exception("Failed to create user");
                }

                await transaction.CommitAsync();

                var authResponse = _mapper.Map<AuthResponseDto>(user)!;
                authResponse = authResponse with { Token = _tokenService.CreateToken(user) };
                return authResponse;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        public async Task<UserDto> GetUserProfileById(string userId)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)
                .Include(u => u.Pets)
                .Include(u => u.Preferences)
                    .ThenInclude(p => p.NotificationSettings)
                .FirstOrDefaultAsync(u => u.Id == userId) ?? throw new KeyNotFoundException("User not found");
            var userDto = _mapper.Map<UserDto>(user);
            return userDto ?? throw new InvalidOperationException("Failed to map user to DTO");
        }

        public async Task<UserDto> IsUserLoggedInByEmail(string email)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)
                .Include(u => u.Preferences)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            var isSignedIn = _signInManager.IsSignedIn(new ClaimsPrincipal(
                new ClaimsIdentity(new[] { new Claim(ClaimTypes.Email, email) })
            ));

            if (!isSignedIn)
                throw new UnauthorizedAccessException("User is not logged in");

            var userDto = _mapper.Map<UserDto>(user);
            return userDto ?? throw new InvalidOperationException("Failed to map user to DTO");
        }


        public async Task<AuthResponseDto> LoginUserAsync(LoginDto loginDto)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email);

            if (user == null)
                throw new UnauthorizedAccessException("Invalid email");

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);
            if (!result.Succeeded)
                throw new UnauthorizedAccessException("Invalid password");

            if (loginDto.Location != null)
                await UpdateUserLocation(user.Id, loginDto.Location);

            var authResponse = _mapper.Map<AuthResponseDto>(user)!;
            authResponse = authResponse with { Token = _tokenService.CreateToken(user) };
            return authResponse;
        }

        public async Task LogoutUser()
        {
            await _signInManager.SignOutAsync();
        }

        public async Task<UserDto> UpdateUserPersonalInfo(string userId, UpdateUserDto updateUserPersonalInfoDto)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)
                .Include(u => u.Pets)
                .Include(u => u.Preferences)
                    .ThenInclude(p => p.NotificationSettings)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException("User not found");

            user.Name = updateUserPersonalInfoDto.Name;

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return _mapper.Map<UserDto>(user)
                ?? throw new InvalidOperationException("Failed to map user to DTO");
        }

        public async Task<UserDto> UpdateUserProfilePicture(string userId, UpdateProfilePictureDto pictureDto)
        {
            if (string.IsNullOrEmpty(_environment.WebRootPath))
                throw new InvalidOperationException("WebRootPath is not configured");

            var user = await _userManager.Users
                .Include(u => u.Location)
                .Include(u => u.Pets)
                .Include(u => u.Preferences)
                    .ThenInclude(p => p.NotificationSettings)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException("User not found");

            // Delete old profile picture if exists
            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                var oldPath = Path.Combine(_environment.WebRootPath, user.ProfilePicturePath);
                if (File.Exists(oldPath))
                    File.Delete(oldPath);
            }

            // Save new profile picture
            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(pictureDto.FileName)}";
            var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadPath);
            var filePath = Path.Combine(uploadPath, fileName);

            // Convert base64 to file
            var imageBytes = Convert.FromBase64String(pictureDto.Base64Data);
            await File.WriteAllBytesAsync(filePath, imageBytes);

            user.ProfilePicturePath = Path.Combine("uploads", "profiles", fileName);
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return _mapper.Map<UserDto>(user)
                ?? throw new InvalidOperationException("Failed to map user to DTO");
        }

        private async Task ValidateProfilePicture(UpdateProfilePictureDto pictureDto)
        {
            if (string.IsNullOrEmpty(pictureDto.Base64Data))
                throw new ArgumentException("Profile picture data is required");

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
            var extension = Path.GetExtension(pictureDto.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                throw new ArgumentException($"Invalid file type. Allowed types are: {string.Join(", ", allowedExtensions)}");

            var maxSizeInBytes = 5 * 1024 * 1024;
            var sizeInBytes = pictureDto.Base64Data.Length * 3 / 4;

            if (sizeInBytes > maxSizeInBytes)
                throw new ArgumentException($"File size exceeds maximum allowed size of {maxSizeInBytes / 1024 / 1024}MB");
        }

        public async Task<string> GetUserProfilePictureName(string id)
        {
            var user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Id == id)
                ?? throw new KeyNotFoundException("User not found");

            if (string.IsNullOrEmpty(user.ProfilePicturePath))
                throw new KeyNotFoundException("Profile picture not found");

            return Path.GetFileName(user.ProfilePicturePath);
        }

        public async Task<UserDto> UpdateUserPreferences(string userId, UpdatePreferencesDto preferencesDto)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)
                .Include(u => u.Preferences)
                    .ThenInclude(p => p.NotificationSettings)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (user.Preferences == null)
            {
                user.Preferences = _mapper.Map<UserPreferences>(preferencesDto);
                if (user.Preferences != null)
                {
                    user.Preferences.UserId = userId;
                }
            }
            else
            {
                _mapper.Map(preferencesDto, user.Preferences);
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return _mapper.Map<UserDto>(user) ?? throw new InvalidOperationException("Failed to map user to DTO");
        }

        public async Task<AuthResponseDto> ValidateAndRefreshTokenAsync(string token)
        {
            var principal = _tokenService.ValidateToken(token);
            if (principal == null)
                throw new UnauthorizedAccessException("Invalid token");

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new UnauthorizedAccessException("Invalid token claims");

            var user = await _userManager.Users
                .Include(u => u.Location)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new UnauthorizedAccessException("User not found");

            // Generate new token
            var authResponse = _mapper.Map<AuthResponseDto>(user)!;
            authResponse = authResponse with { Token = _tokenService.CreateToken(user) };

            return authResponse;
        }

        public async Task<UserDto> UpdateUserLocation(string userId, LocationDto locationDto)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
                throw new KeyNotFoundException("User not found");

            if (user.Location == null)
            {
                user.Location = _mapper.Map<Location>(locationDto);
                if (user.Location != null)
                {
                    user.Location.CreatedAt = DateTime.UtcNow;
                }
            }
            else
            {
                _mapper.Map(locationDto, user.Location);
            }

            await _context.SaveChangesAsync();
            return _mapper.Map<UserDto>(user) ?? throw new InvalidOperationException("Failed to map user to DTO");
        }

        

        public async Task UpdateUserOnlineStatus(string userId, bool isOnline)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.LastSeen = DateTime.UtcNow;
                user.IsOnline = isOnline;
                await _userManager.UpdateAsync(user);
            }
        }
    }
}
