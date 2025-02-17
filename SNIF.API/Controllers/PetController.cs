// SNIF.API/Controllers/PetController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using SNIF.API.Extensions;
using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using System.Security.Claims;
using MediaType = SNIF.Core.DTOs.MediaType;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/pets")]
    public class PetController : ControllerBase
    {
        private readonly IPetService _petService;
        private readonly IWebHostEnvironment _environment;

        public PetController(IPetService petService, IWebHostEnvironment environment)
        {
            _petService = petService;
            _environment = environment;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<PetDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PetDto>>> GetPets(
            [FromQuery] string userId,
            [FromQuery] PetPurpose? purpose,
            [FromQuery] string? species)
        {
            var pets = await _petService.GetUserPetsAsync(userId);
            return Ok(pets);
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PetDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<PetDto>> GetPet(string id)
        {
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
        [Authorize]
        [ProducesResponseType(typeof(PetDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<PetDto>> CreatePet([FromBody] CreatePetDto createPetDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? throw new UnauthorizedAccessException();

            var pet = await _petService.CreatePetAsync(userId, createPetDto);
            return CreatedAtAction(nameof(GetPet), new { id = pet.Id }, pet);
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


        [HttpPost("{id}/media")]
        [Authorize]
        [ProducesResponseType(typeof(MediaResponseDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<MediaResponseDto>> AddMedia(
            string id,
            [FromBody] AddMediaDto mediaDto)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var result = await _petService.AddPetMediaAsync(id, mediaDto, baseUrl);
                return CreatedAtAction(
                    nameof(GetMediaInfo),
                    new { id, mediaId = result.Id },
                    result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponse { Message = ex.Message });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        [HttpGet("{id}/media")]
        [ProducesResponseType(typeof(IEnumerable<MediaResponseDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<MediaResponseDto>>> GetMedia(
            string id,
            [FromQuery] MediaType? type)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var media = await _petService.GetPetMediaAsync(id, type, baseUrl);
                return Ok(media);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        [HttpGet("media/{mediaId}")]
        [ProducesResponseType(typeof(MediaResponseDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MediaResponseDto>> GetMediaInfo(string mediaId)
        {
            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var media = await _petService.GetMediaByIdAsync(mediaId, baseUrl);
                return Ok(media);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Media not found" });
            }
        }

        [HttpDelete("{id}/media/{mediaId}")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteMedia(string id, string mediaId)
        {
            try
            {
                await _petService.DeletePetMediaAsync(id, mediaId);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Media not found" });
            }
        }
    }
}