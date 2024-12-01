using Microsoft.AspNetCore.Hosting;
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

        public UserService(ITokenService tokenService, SignInManager<User> signInManager, UserManager<User> userManager, SNIFContext context, IWebHostEnvironment environment)
        {
            _tokenService = tokenService;
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _environment = environment;
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
                var location = new Location
                {
                    Latitude = createUserDto.Location.Latitude,
                    Longitude = createUserDto.Location.Longitude,
                    Address = createUserDto.Location.Address,
                    City = createUserDto.Location.City,
                    Country = createUserDto.Location.Country,
                    CreatedAt = DateTime.UtcNow
                };

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
                return new AuthResponseDto
                {
                    Id = user.Id,
                    Email = user.Email!,
                    Name = user.Name,
                    Token = _tokenService.CreateToken(user),
                    CreatedAt = user.CreatedAt
                };
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
            .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new Exception("User not found");
            }

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                Name = user.Name,
                Location = user.Location == null ? null : new LocationDto
                {
                    Latitude = user.Location.Latitude,
                    Longitude = user.Location.Longitude,
                    Address = user.Location.Address,
                    City = user.Location.City,
                    Country = user.Location.Country
                },
                Pets = user.Pets.Select(p => new PetDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Species = p.Species,
                    Breed = p.Breed
                }).ToList(),
                BreederVerification = user.BreederVerification,
                Preferences = user.Preferences,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                ProfilePicturePath = user.ProfilePicturePath != null ?
                    $"/api/user/profile-picture/{Path.GetFileName(user.ProfilePicturePath)}" : null
            };
        }

        public async Task<UserDto> IsUserLoggedInByEmail(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                throw new UnauthorizedAccessException("User not found");
            }

            var isSignedIn = _signInManager.IsSignedIn(new ClaimsPrincipal(
                new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.Email, email)
                })
            ));

            if (!isSignedIn)
            {
                throw new UnauthorizedAccessException("User is not logged in");
            }

            return new UserDto
            {
                Email = user.Email!,
                Name = user.Name,
                Location = user.Location == null ? null : new LocationDto
                {
                    Latitude = user.Location.Latitude,
                    Longitude = user.Location.Longitude,
                    Address = user.Location.Address,
                    City = user.Location.City,
                    Country = user.Location.Country
                },
            };
        }

        public async Task<AuthResponseDto> LoginUserAsync(LoginDto loginDto)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)
                .FirstOrDefaultAsync(u => u.Email == loginDto.Email);
            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid email");
            }

            var result = _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false).Result;
            if (!result.Succeeded)
            {
                throw new UnauthorizedAccessException("Invalid password");
            }

            await UpdateUserLocation(user.Id, loginDto.Location);

            return new AuthResponseDto
            {
                Id = user.Id,
                Email = user.Email!,
                Name = user.Name,
                Token = _tokenService.CreateToken(user),
                CreatedAt = user.CreatedAt
            };
        }

        public async Task LogoutUser()
        {
            await _signInManager.SignOutAsync();
        }

        public async Task<UserDto> UpdateUserPersonalInfo(string userId, UpdateUserPersonalInfoDto updateUserPersonalInfoDto)
        {
            if (string.IsNullOrEmpty(_environment.WebRootPath))
            {
                throw new InvalidOperationException("WebRootPath is not configured");
            }

            var user = await _userManager.Users
                .Include(u => u.Location)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            if (!string.IsNullOrEmpty(updateUserPersonalInfoDto.Name))
            {
                user.Name = updateUserPersonalInfoDto.Name;
            }

            if (updateUserPersonalInfoDto.ProfilePicture != null)
            {
                // Delete old profile picture if exists
                if (!string.IsNullOrEmpty(user.ProfilePicturePath))
                {
                    var oldPath = Path.Combine(_environment.WebRootPath, user.ProfilePicturePath);
                    if (File.Exists(oldPath))
                    {
                        File.Delete(oldPath);
                    }
                }

                // Save new profile picture
                var fileName = $"{Guid.NewGuid()}{Path.GetExtension(updateUserPersonalInfoDto.ProfilePicture.FileName)}";
                var uploadPath = Path.Combine(_environment.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(uploadPath);
                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await updateUserPersonalInfoDto.ProfilePicture.CopyToAsync(stream);
                }

                user.ProfilePicturePath = Path.Combine("uploads", "profiles", fileName);
            }

            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                Name = user.Name,
                ProfilePicturePath = user.ProfilePicturePath != null ?
                    $"/api/user/profile-picture/{Path.GetFileName(user.ProfilePicturePath)}" : null,
                Location = new LocationDto
                {
                    Latitude = user.Location.Latitude,
                    Longitude = user.Location.Longitude,
                    Address = user.Location.Address,
                    City = user.Location.City,
                    Country = user.Location.Country
                },
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt
            };
        }

        public async Task<UserDto> UpdateUserLocation(string userId, LocationDto locationDto)
        {
            var user = await _userManager.Users
                .Include(u => u.Location)  // Include Location
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new KeyNotFoundException("User not found");
            }

            if (user.Location == null)
            {
                var newLocation = new Location
                {
                    Latitude = locationDto.Latitude,
                    Longitude = locationDto.Longitude,
                    Address = locationDto.Address,
                    City = locationDto.City,
                    Country = locationDto.Country,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Locations.Add(newLocation);
                user.Location = newLocation;
            }
            else
            {
                if (user.Location.Latitude != locationDto.Latitude ||
                    user.Location.Longitude != locationDto.Longitude ||
                    user.Location.Address != locationDto.Address ||
                    user.Location.City != locationDto.City ||
                    user.Location.Country != locationDto.Country)
                {
                    user.Location.Latitude = locationDto.Latitude;
                    user.Location.Longitude = locationDto.Longitude;
                    user.Location.Address = locationDto.Address;
                    user.Location.City = locationDto.City;
                    user.Location.Country = locationDto.Country;
                    user.Location.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            return new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                Name = user.Name,
                Location = new LocationDto
                {
                    Latitude = user.Location.Latitude,
                    Longitude = user.Location.Longitude,
                    Address = user.Location.Address,
                    City = user.Location.City,
                    Country = user.Location.Country
                },
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                ProfilePicturePath = user.ProfilePicturePath != null ?
                    $"/api/user/profile-picture/{Path.GetFileName(user.ProfilePicturePath)}" : null
            };
        }
    }
}
