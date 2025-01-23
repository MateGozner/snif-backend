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
    [Route("api/[controller]")]
    public class MatchController : ControllerBase
    {
        private readonly IMatchService _matchService;

        public MatchController(IMatchService matchService)
        {
            _matchService = matchService;
        }

        [HttpGet("pet/{petId}")]
        [ProducesResponseType(typeof(IEnumerable<MatchDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<MatchDto>>> GetPetMatches(string petId)
        {
            if (string.IsNullOrEmpty(petId))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            try
            {
                var matches = await _matchService.GetPetMatchesAsync(petId);
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

        [HttpGet("pet/{petId}/potential")]
        [ProducesResponseType(typeof(IEnumerable<PetDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<IEnumerable<PetDto>>> GetPotentialMatches(string petId, [FromQuery] PetPurpose purpose)
        {
            if (string.IsNullOrEmpty(petId))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            try
            {
                var matches = await _matchService.GetPotentialMatchesAsync(petId, purpose);
                return Ok(matches);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        [HttpGet("{matchId}")]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MatchDto>> GetMatch(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
                return BadRequest(new ErrorResponse { Message = "Match ID is required" });

            try
            {
                var match = await _matchService.GetMatchByIdAsync(matchId);
                return Ok(match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Match not found" });
            }
        }


        [HttpPost("pet/{petId}/match")]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<MatchDto>> CreateMatch(string petId, [FromBody] CreateMatchDto createMatchDto)
        {
            if (string.IsNullOrEmpty(petId))
                return BadRequest(new ErrorResponse { Message = "Pet ID is required" });

            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid match data" });

            try
            {
                var match = await _matchService.CreateMatchAsync(petId, createMatchDto);
                return CreatedAtAction(nameof(GetMatch), new { matchId = match.Id }, match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Pet not found" });
            }
        }

        [HttpPut("{matchId}/status")]
        [ProducesResponseType(typeof(MatchDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MatchDto>> UpdateMatchStatus(string matchId, [FromBody] UpdateMatchDto updateMatchDto)
        {
            if (string.IsNullOrEmpty(matchId))
                return BadRequest(new ErrorResponse { Message = "Match ID is required" });

            if (!ModelState.IsValid)
                return BadRequest(new ErrorResponse { Message = "Invalid status data" });

            try
            {
                var match = await _matchService.UpdateMatchStatusAsync(matchId, updateMatchDto.Status);
                return Ok(match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Match not found" });
            }
        }

        [HttpDelete("{matchId}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> DeleteMatch(string matchId)
        {
            if (string.IsNullOrEmpty(matchId))
                return BadRequest(new ErrorResponse { Message = "Match ID is required" });

            try
            {
                await _matchService.DeleteMatchAsync(matchId);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ErrorResponse { Message = "Match not found" });
            }
        }
    }
}