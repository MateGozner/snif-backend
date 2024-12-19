// SNIF.API/Controllers/MatchController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using System.Security.Claims;

namespace SNIF.API.Controllers
{
    //[Authorize]
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
        public async Task<ActionResult<IEnumerable<MatchDto>>> GetPetMatches(string petId)
        {
            try
            {
                var matches = await _matchService.GetPetMatchesAsync(petId);
                return Ok(matches);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("pet/{petId}/pending")]
        public async Task<ActionResult<IEnumerable<MatchDto>>> GetPendingMatches(string petId)
        {
            try
            {
                var matches = await _matchService.GetPendingMatchesForPetAsync(petId);
                return Ok(matches);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("pet/{petId}/potential")]
        public async Task<ActionResult<IEnumerable<PetDto>>> GetPotentialMatches(
            string petId,
            [FromQuery] PetPurpose purpose)
        {
            try
            {
                var matches = await _matchService.GetPotentialMatchesAsync(petId, purpose);
                return Ok(matches);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{matchId}")]
        public async Task<ActionResult<MatchDto>> GetMatch(string matchId)
        {
            try
            {
                var match = await _matchService.GetMatchByIdAsync(matchId);
                return Ok(match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("pet/{petId}/match")]
        public async Task<ActionResult<MatchDto>> CreateMatch(
            string petId,
            [FromBody] CreateMatchDto createMatchDto)
        {
            try
            {
                var match = await _matchService.CreateMatchAsync(petId, createMatchDto);
                return CreatedAtAction(
                    nameof(GetMatch),
                    new { matchId = match.Id },
                    match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPut("{matchId}/status")]
        public async Task<ActionResult<MatchDto>> UpdateMatchStatus(
            string matchId,
            [FromBody] UpdateMatchDto updateMatchDto)
        {
            try
            {
                var match = await _matchService.UpdateMatchStatusAsync(matchId, updateMatchDto.Status);
                return Ok(match);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpDelete("{matchId}")]
        public async Task<IActionResult> DeleteMatch(string matchId)
        {
            try
            {
                await _matchService.DeleteMatchAsync(matchId);
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }
    }
}