using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNIF.API.Extensions;
using SNIF.Core.DTOs;
using SNIF.Core.Interfaces;
using System.Net;

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
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        [HttpPost("register")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponseDto>> Register(CreateUserDto createUserDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid input data" });

            try
            {
                var user = await _userService.RegisterUserAsync(createUserDto);
                return Ok(user);
            }
            catch (UnauthorizedAccessException e)
            {
                return Unauthorized(new ErrorResponse { Message = e.Message });
            }
        }

        [HttpPost("login")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponseDto>> Login(LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid login data" });

            try
            {
                var user = await _userService.LoginUserAsync(loginDto);
                return Ok(user);
            }
            catch (UnauthorizedAccessException e)
            {
                return Unauthorized(new ErrorResponse { Message = e.Message });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> Logout()
        {
            await _userService.LogoutUser();
            return NoContent();
        }

        [HttpGet("profile/{id}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> GetProfileById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new ErrorResponse { Message = "Invalid user id" });

            try
            {
                var profile = await _userService.GetUserProfileById(id);
                return Ok(profile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }

        [Authorize]
        [HttpGet("check-auth/{email}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UserDto>> CheckAuthByEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return BadRequest(new ErrorResponse { Message = "Email is required" });

            try
            {
                var user = await _userService.IsUserLoggedInByEmail(email);
                return Ok(user);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new ErrorResponse { Message = ex.Message });
            }
        }

        [HttpPut("profile/{id}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> UpdateProfile(string id, UpdateUserPersonalInfoDto updateUserPersonalInfoDto)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new ErrorResponse { Message = "Invalid user id" });

            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid update data" });

            try
            {
                var updatedProfile = await _userService.UpdateUserPersonalInfo(id, updateUserPersonalInfoDto);
                return Ok(updatedProfile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }

        [HttpGet("profile-picture/{fileName}")]
        [ProducesResponseType(typeof(PhysicalFileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public IActionResult GetProfilePicture(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return BadRequest(new ErrorResponse { Message = "File name is required" });

            var path = Path.Combine(_environment.WebRootPath, "uploads", "profiles", fileName);
            if (!System.IO.File.Exists(path))
                return NotFound(new ErrorResponse { Message = "Profile picture not found" });

            return PhysicalFile(path, "image/jpeg");
        }

        [Authorize]
        [HttpPut("preferences/{id}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> UpdatePreferences(string id, UpdatePreferencesDto preferencesDto)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new ErrorResponse { Message = "Invalid user id" });

            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid preferences data" });

            try
            {
                var updatedUser = await _userService.UpdateUserPreferences(id, preferencesDto);
                return Ok(updatedUser);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }

        [HttpPost("validate-token")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<AuthResponseDto>> ValidateToken([FromBody] TokenValidationDto tokenDto)
        {
            try
            {
                var response = await _userService.ValidateAndRefreshTokenAsync(tokenDto.Token);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new ErrorResponse { Message = ex.Message });
            }
        }
    }
}