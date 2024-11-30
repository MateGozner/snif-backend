using Microsoft.AspNetCore.Identity;
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

        public UserService(ITokenService tokenService, SignInManager<User> signInManager, UserManager<User> userManager, SNIFContext context)
        {
            _tokenService = tokenService;
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
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
                    CreatedAt = DateTime.UtcNow
                    // Set other location properties as needed
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
                    Email = user.Email!,
                    Name = user.Name,
                    Token = _tokenService.CreateToken(user)
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
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                throw new Exception("User not found");
            }

            if (user.Location == null)
            {
                throw new Exception("User location not found");
            }

            return new UserDto
            {
                Email = user.Email!,
                Name = user.Name,
                Location = user.Location
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
                Location = user.Location
            };
        }

        public Task<AuthResponseDto> LoginUserAsync(LoginDto loginDto)
        {
            var user = _userManager.FindByEmailAsync(loginDto.Email).Result;
            if (user == null)
            {
                throw new UnauthorizedAccessException("Invalid email");
            }

            var result = _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false).Result;
            if (!result.Succeeded)
            {
                throw new UnauthorizedAccessException("Invalid password");
            }

            return Task.FromResult(new AuthResponseDto
            {
                Email = user.Email!,
                Name = user.Name,
                Token = _tokenService.CreateToken(user)
            });
        }

        public async Task LogoutUser()
        {
            await _signInManager.SignOutAsync();
        }
    }
}
