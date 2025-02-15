// SNIF.API/Controllers/MatchController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public class MatchController : ControllerBase
    {
        private readonly IMatchService _matchService;

        public MatchController(IMatchService matchService)
        {
            _matchService = matchService;
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

            try
            {
                var matches = status switch
                {
                    MatchStatus.Pending => await _matchService.GetPendingMatchesForPetAsync(petId),
                    null => await _matchService.GetPetMatchesAsync(petId),
                    _ => throw new ArgumentException("Invalid status")
                };

                Response.Headers.Append("Cache-Control", "private, max-age=60");
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

            try
            {
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

            try
            {
                var matches = await _matchService.GetPotentialMatchesAsync(petId, purpose);
                Response.Headers.Append("Cache-Control", "private, max-age=300"); // 5 minutes
                return Ok(matches);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        // GET api/matches/{id}
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MatchDto>> GetMatch(string id)
        {
            try
            {
                var match = await _matchService.GetMatchByIdAsync(id);
                Response.Headers.Append("Cache-Control", "private, max-age=60");
                return Ok(match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Match not found" });
            }
        }



        // POST api/matches
        [HttpPost]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status201Created)]
        public async Task<ActionResult<MatchDto>> CreateMatch([FromBody] CreateMatchDto createMatchDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid match data" });

            try
            {
                var match = await _matchService.CreateMatchAsync(createMatchDto);
                return CreatedAtAction(nameof(GetMatch), new { id = match.Id }, match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        // PATCH api/matches/{id}
        [HttpPatch("{id}")]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<MatchDto>> UpdateMatchStatus(
            string id,
            [FromBody] UpdateMatchStatusDto updateDto)
        {
            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid status data" });

            try
            {
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

            var ids = petIds.Split(',').Where(id => !string.IsNullOrEmpty(id)).ToList();

            try
            {
                var matches = await _matchService.GetBulkMatchesAsync(ids, status);
                Response.Headers.Append("Cache-Control", "private, max-age=60");
                return Ok(matches);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new ErrorResponse { Message = ex.Message });
            }
        }


        // DELETE api/matches/{id}
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        public async Task<IActionResult> DeleteMatch(string id)
        {
            try
            {
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