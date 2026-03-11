// SNIF.API/Controllers/MatchController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SNIF.API.Attributes;
using SNIF.API.Extensions;
using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using System.Security.Claims;

namespace SNIF.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/matches")]
    [EnableRateLimiting("swipe")]
    public class MatchController : ControllerBase
    {
        private readonly IMatchService _matchService;
        private readonly IEntitlementService _entitlementService;
        private readonly IPetService _petService;

        public MatchController(IMatchService matchService, IEntitlementService entitlementService, IPetService petService)
        {
            _matchService = matchService;
            _entitlementService = entitlementService;
            _petService = petService;
        }

        private async Task<ActionResult<MatchDto>> CreateMatchInternal(CreateMatchDto createMatchDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid match data" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ErrorResponse { Message = "User not authenticated" });

            try
            {
                var match = await _matchService.CreateMatchAsync(userId, createMatchDto);
                return CreatedAtAction(nameof(GetMatch), new { id = match.Id }, match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = ex.Message });
            }
        }

        // GET api/matches?petId={petId}&status={status}
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<MatchDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<MatchDto>>> GetMatches(
            [FromQuery] string petId,
            [FromQuery] MatchStatus? status = null)
        {
            if (string.IsNullOrEmpty(petId))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            var authUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(authUserId))
                return Unauthorized(new ErrorResponse { Message = "User not authenticated" });

            try
            {
                var pet = await _petService.GetPetByIdAsync(petId);
                if (pet.OwnerId != authUserId)
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "You can only view matches for your own pets" });

                var matches = status switch
                {
                    MatchStatus.Pending => await _matchService.GetPendingMatchesForPetAsync(petId),
                    null => await _matchService.GetPetMatchesAsync(petId),
                    _ => throw new ArgumentException("Invalid status")
                };

                return Ok(matches);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }


        [HttpGet("pet/{petId}/pending")]
        [ProducesResponseType(typeof(IEnumerable<MatchDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<MatchDto>>> GetPendingMatches(string petId)
        {
            if (string.IsNullOrEmpty(petId))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            var authUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(authUserId))
                return Unauthorized(new ErrorResponse { Message = "User not authenticated" });

            try
            {
                var pet = await _petService.GetPetByIdAsync(petId);
                if (pet.OwnerId != authUserId)
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "You can only view matches for your own pets" });

                var matches = await _matchService.GetPendingMatchesForPetAsync(petId);
                return Ok(matches);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        // GET api/matches/potential?petId={petId}&purpose={purpose}
        [HttpGet("potential")]
        [ProducesResponseType(typeof(IEnumerable<PetDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<PetDto>>> GetPotentialMatches(
            [FromQuery] string petId,
            [FromQuery] PetPurpose? purpose)
        {
            if (string.IsNullOrEmpty(petId))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ErrorResponse { Message = "User not authenticated" });

            try
            {
                var matches = await _matchService.GetPotentialMatchesAsync(userId, petId, purpose);
                return Ok(matches);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = ex.Message });
            }
        }

        // GET api/matches/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MatchDto>> GetMatch(string id)
        {
            var authUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(authUserId))
                return Unauthorized(new ErrorResponse { Message = "User not authenticated" });

            try
            {
                var match = await _matchService.GetMatchByIdAsync(id);
                if (match.InitiatorPet.OwnerId != authUserId && match.TargetPet.OwnerId != authUserId)
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "You can only view your own matches" });

                return Ok(match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Match not found" });
            }
        }



        // POST api/matches
        [HttpPost]
        [EnforceUsageLimit(UsageType.Like)]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<MatchDto>> CreateMatch([FromBody] CreateMatchDto createMatchDto)
        {
            return await CreateMatchInternal(createMatchDto);
        }

        // POST api/matches/super-sniff
        [HttpPost("super-sniff")]
        [EnforceUsageLimit(UsageType.SuperSniff)]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<MatchDto>> CreateSuperSniff([FromBody] CreateMatchDto createMatchDto)
        {
            return await CreateMatchInternal(createMatchDto);
        }

        // PATCH api/matches/{id}
        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<MatchDto>> UpdateMatchStatus(
            string id,
            [FromBody] UpdateMatchStatusDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid status data" });

            var authUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            try
            {
                var existingMatch = await _matchService.GetMatchByIdAsync(id);
                if (existingMatch.InitiatorPet.OwnerId != authUserId && existingMatch.TargetPet.OwnerId != authUserId)
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "You can only update matches involving your own pets" });

                var match = await _matchService.UpdateMatchStatusAsync(id, updateDto.Status);
                return Ok(match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Match not found" });
            }
        }

        [HttpGet("bulk")]
        [ProducesResponseType(typeof(IDictionary<string, IEnumerable<MatchDto>>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IDictionary<string, IEnumerable<MatchDto>>>> GetBulkMatches(
    [FromQuery] string petIds,
    [FromQuery] MatchStatus? status = null)
        {
            if (string.IsNullOrEmpty(petIds))
                return BadRequest(new ErrorResponse { Message = "Pet IDs are required" });

            var authUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(authUserId))
                return Unauthorized(new ErrorResponse { Message = "User not authenticated" });

            var ids = petIds.Split(',').Where(id => !string.IsNullOrEmpty(id)).ToList();

            // Verify all requested pets belong to the authenticated user
            foreach (var petId in ids)
            {
                var pet = await _petService.GetPetByIdAsync(petId);
                if (pet.OwnerId != authUserId)
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "You can only view matches for your own pets" });
            }

            try
            {
                var matches = await _matchService.GetBulkMatchesAsync(ids, status);
                return Ok(matches);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }


        [HttpGet("who-liked-me")]
        [ProducesResponseType(typeof(List<WhoLikedYouDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetWhoLikedMe()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var entitlement = await _entitlementService.GetEntitlementAsync(userId);

            var result = await _matchService.GetWhoLikedYouAsync(userId, entitlement.EffectivePlan);
            return Ok(result);
        }

        // DELETE api/matches/{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteMatch(string id)
        {
            var authUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            try
            {
                var match = await _matchService.GetMatchByIdAsync(id);
                if (match.InitiatorPet.OwnerId != authUserId && match.TargetPet.OwnerId != authUserId)
                    return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse { Message = "You can only delete matches involving your own pets" });

                await _matchService.DeleteMatchAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Match not found" });
            }
        }
    }
}