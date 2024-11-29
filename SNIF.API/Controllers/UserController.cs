
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Infrastructure.Data;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ITokenService _tokenService;

        private readonly SNIFContext _context;

        public UserController(UserManager<User> userManager, SignInManager<User> signInManager, ITokenService tokenService, SNIFContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context;
        }
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(CreateUserDto createUserDto)
        {
            if (await _userManager.FindByEmailAsync(createUserDto.Email) != null)
            {
                return BadRequest("Email already registered");
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
                    await transaction.RollbackAsync();
                    return BadRequest(result.Errors);
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
                if (transaction.GetDbTransaction().Connection != null)
                {
                    await transaction.RollbackAsync();
                }

                throw;
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
        {
            var user = await _userManager.FindByEmailAsync(loginDto.Email);

            if (user == null)
            {
                return Unauthorized("Invalid email");
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);

            if (!result.Succeeded)
            {
                return Unauthorized("Invalid password");
            }

            return new AuthResponseDto
            {
                Email = user.Email!,
                Name = user.Name,
                Token = _tokenService.CreateToken(user)
            };
        }

        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return NoContent();
        }
    }


}