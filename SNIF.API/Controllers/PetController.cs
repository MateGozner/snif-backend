// SNIF.API/Controllers/PetController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNIF.API.Extensions;
using SNIF.Core.DTOs;
using SNIF.Core.Interfaces;
using System.Security.Claims;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PetController : ControllerBase
    {
        private readonly IPetService _petService;
        private readonly IWebHostEnvironment _environment;

        public PetController(IPetService petService, IWebHostEnvironment environment)
        {
            _petService = petService;
            _environment = environment;
        }

        [HttpGet("user/{userId}")]
        [ProducesResponseType(typeof(IEnumerable<PetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<IEnumerable<PetDto>>> GetUserPets(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest(new ErrorResponse { Message = "User ID is required" });

            var pets = await _petService.GetUserPetsAsync(userId);
            return Ok(pets);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PetDto>> GetPet(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            try
            {
                var pet = await _petService.GetPetByIdAsync(id);
                return Ok(pet);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(PetDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<PetDto>> CreatePet(CreatePetDto createPetDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid pet data" });

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ErrorResponse { Message = "User not authenticated" });

            try
            {
                var pet = await _petService.CreatePetAsync(userId, createPetDto);
                return CreatedAtAction(nameof(GetPet), new { id = pet.Id }, pet);
            }
            catch (Exception ex)
            {
                return BadRequest(new ErrorResponse { Message = ex.Message });
            }
        }


        [HttpPut("{id}")]
        [ProducesResponseType(typeof(PetDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<PetDto>> UpdatePet(string id, [FromBody] UpdatePetDto updatePetDto)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid update data" });

            try
            {
                var pet = await _petService.UpdatePetAsync(id, updatePetDto);
                return Ok(pet);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeletePet(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            try
            {
                await _petService.DeletePetAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }


        [HttpPost("{id}/photos")]
        public async Task<IActionResult> AddPhoto(string id, IFormFile photo)
        {
            try
            {
                var fileName = await _petService.AddPetPhotoAsync(id, photo);
                return Ok(new { FileName = fileName });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("{id}/videos")]
        public async Task<IActionResult> AddVideo(string id, IFormFile video)
        {
            try
            {
                var fileName = await _petService.AddPetVideoAsync(id, video);
                return Ok(new { FileName = fileName });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}/photos/{fileName}")]
        public async Task<IActionResult> DeletePhoto(string id, string fileName)
        {
            try
            {
                await _petService.DeletePetPhotoAsync(id, fileName);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}/videos/{fileName}")]
        public async Task<IActionResult> DeleteVideo(string id, string fileName)
        {
            try
            {
                await _petService.DeletePetVideoAsync(id, fileName);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("photos/{fileName}")]
        public IActionResult GetPhoto(string fileName)
        {
            var path = Path.Combine(_environment.WebRootPath, "uploads", "pets", "photos", fileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var fileExtension = Path.GetExtension(fileName).ToLower();
            var contentType = fileExtension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "application/octet-stream"
            };

            return PhysicalFile(path, contentType);
        }

        [HttpGet("videos/{fileName}")]
        public IActionResult GetVideo(string fileName)
        {
            var path = Path.Combine(_environment.WebRootPath, "uploads", "pets", "videos", fileName);
            if (!System.IO.File.Exists(path))
            {
                return NotFound();
            }

            var fileExtension = Path.GetExtension(fileName).ToLower();
            var contentType = fileExtension switch
            {
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };

            return PhysicalFile(path, contentType);
        }
    }
}