
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Storage;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Interfaces;
using SNIF.Core.Models;
using SNIF.Infrastructure.Data;
using System.Security.Claims;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IWebHostEnvironment _environment;

        public UserController(IUserService userService, IWebHostEnvironment environment)
        {
            _userService = userService;
            _environment = environment;
        }
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponseDto>> Register(CreateUserDto createUserDto)
        {
            try
            {
                var user = await _userService.RegisterUserAsync(createUserDto);
                return Ok(user);
            }
            catch (UnauthorizedAccessException e)
            {
                return Unauthorized(e.Message);
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
        {
            try
            {
                var user = await _userService.LoginUserAsync(loginDto);
                return Ok(user);
            }
            catch (UnauthorizedAccessException e)
            {
                return Unauthorized(e.Message);
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<ActionResult> Logout()
        {
            await _userService.LogoutUser();
            return NoContent();
        }

        [HttpGet("profile/{id}")]
        public async Task<ActionResult<UserDto>> GetProfileById(string id)
        {
            try
            {
                var profile = await _userService.GetUserProfileById(id);
                return Ok(profile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [Authorize]
        [HttpGet("check-auth/{email}")]
        public async Task<ActionResult<UserDto>> CheckAuthByEmail(string email)
        {
            try
            {
                var user = await _userService.IsUserLoggedInByEmail(email);
                return Ok(user);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
        }

        [HttpPut("profile/{id}")]
        public async Task<ActionResult<UserDto>> UpdateProfile(string id, UpdateUserPersonalInfoDto updateUserPersonalInfoDto)
        {
            try
            {
                var updatedProfile = await _userService.UpdateUserPersonalInfo(id, updateUserPersonalInfoDto);
                return Ok(updatedProfile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet("profile-picture/{filename}")]
        public IActionResult GetProfilePicture(string fileName)
        {
            var path = Path.Combine(_environment.WebRootPath, "uploads", "profiles", fileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            return PhysicalFile(path, "image/jpeg");
        }

        [Authorize]
        [HttpPut("preferences/{id}")]
        public async Task<ActionResult<UserDto>> UpdatePreferences(string id, UpdatePreferencesDto preferencesDto)
        {
            try
            {
                var updatedUser = await _userService.UpdateUserPreferences(id, preferencesDto);
                return Ok(updatedUser);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }
    }


}