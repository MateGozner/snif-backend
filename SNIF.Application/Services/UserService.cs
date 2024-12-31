using AutoMapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Infrastructure.Data;
using System;
using System.Collections.Generic;
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

        public async Task<UserDto> UpdateUserPersonalInfo(string userId, UpdateUserPersonalInfoDto updateUserPersonalInfoDto)
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

            if (!string.IsNullOrEmpty(updateUserPersonalInfoDto.Name))
                user.Name = updateUserPersonalInfoDto.Name;

            if (updateUserPersonalInfoDto.ProfilePicture != null)
            {
                await UpdateProfilePicture(user, updateUserPersonalInfoDto.ProfilePicture);
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return _mapper.Map<UserDto>(user) ?? throw new InvalidOperationException("Failed to map user to DTO");
        }


        private async Task UpdateProfilePicture(User user, IFormFile profilePicture)
        {
            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                var oldPath = Path.Combine(_environment.WebRootPath, user.ProfilePicturePath);
                if (File.Exists(oldPath))
                    File.Delete(oldPath);
            }

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(profilePicture.FileName)}";
            var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
            Directory.CreateDirectory(uploadPath);
            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await profilePicture.CopyToAsync(stream);
            }

            user.ProfilePicturePath = Path.Combine("uploads", "profiles", fileName);
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
