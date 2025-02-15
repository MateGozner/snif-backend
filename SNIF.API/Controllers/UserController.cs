using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNIF.Core.DTOs;
using SNIF.Core.Interfaces;
using Microsoft.Net.Http.Headers;
using SNIF.API.Extensions;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IWebHostEnvironment _environment;

        public UserController(IUserService userService, IWebHostEnvironment environment)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        // POST api/users
        [HttpPost]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<AuthResponseDto>> CreateUser(CreateUserDto createUserDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid input data" });

            try
            {
                var user = await _userService.RegisterUserAsync(createUserDto);
                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
            }
            catch (UnauthorizedAccessException e)
            {
                return BadRequest(new ErrorResponse { Message = e.Message });
            }
        }

        // POST api/users/token
        [HttpPost("token")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponseDto>> CreateToken(LoginDto loginDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid login data" });

            try
            {
                var authResponse = await _userService.LoginUserAsync(loginDto);
                Response.Headers.Append("Cache-Control", "no-store");
                return Ok(authResponse);
            }
            catch (UnauthorizedAccessException e)
            {
                return Unauthorized(new ErrorResponse { Message = e.Message });
            }
        }

        // DELETE api/users/token
        [Authorize]
        [HttpDelete("token")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<ActionResult> DeleteToken()
        {
            await _userService.LogoutUser();
            Response.Headers.Append("Cache-Control", "no-store");
            return NoContent();
        }

        // GET api/users/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> GetUser(string id)
        {
            try
            {
                var profile = await _userService.GetUserProfileById(id);
                Response.Headers.Append("Cache-Control", "private, max-age=3600");
                return Ok(profile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }

        // PUT api/users/{id}
        [Authorize]
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> UpdateUser(string id, UpdateUserDto updateUserDto)
        {
            try
            {
                var updatedProfile = await _userService.UpdateUserPersonalInfo(id, updateUserDto);
                Response.Headers.Append("Cache-Control", "no-cache");
                return Ok(updatedProfile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }

        // PUT api/users/{id}/picture
        [Authorize]
        [HttpPut("{id}/picture")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> UpdateUserPicture(string id, UpdateProfilePictureDto pictureDto)
        {
            try
            {
                var updatedProfile = await _userService.UpdateUserProfilePicture(id, pictureDto);
                Response.Headers.Append("Cache-Control", "no-cache");
                return Ok(updatedProfile);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }


        // GET api/users/{id}/picture
        [HttpGet("{id}/picture")]
        [ProducesResponseType(typeof(PhysicalFileResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ProfilePictureResponseDto>> GetUserPicture(string id)
        {
            try
            {
                var fileName = await _userService.GetUserProfilePictureName(id);
                var url = $"{Request.Scheme}://{Request.Host}/uploads/profiles/{fileName}";

                Response.Headers.Append("Cache-Control", "public, max-age=86400");
                return Ok(new ProfilePictureResponseDto { Url = url });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }

        // PUT api/users/{id}/preferences
        [Authorize]
        [HttpPut("{id}/preferences")]
        [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<UserDto>> UpdateUserPreferences(string id, UpdatePreferencesDto preferencesDto)
        {
            try
            {
                var updatedUser = await _userService.UpdateUserPreferences(id, preferencesDto);
                Response.Headers.Append("Cache-Control", "no-cache");
                return Ok(updatedUser);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }

        // POST api/users/token/validate
        [HttpPost("token/validate")]
        [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<AuthResponseDto>> ValidateToken([FromBody] TokenValidationDto tokenDto)
        {
            try
            {
                var response = await _userService.ValidateAndRefreshTokenAsync(tokenDto.Token);
                Response.Headers.Append("Cache-Control", "no-store");
                return Ok(response);
            }
            catch (Exception ex)
            {
                return Unauthorized(new ErrorResponse { Message = ex.Message });
            }
        }
    }
}