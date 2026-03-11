using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SNIF.Core.DTOs;
using SNIF.Core.Entities;
using SNIF.Core.Enums;
using SNIF.Infrastructure.Data;
using System.Security.Claims;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    [EnableRateLimiting("global")]
    public class ReportController : ControllerBase
    {
        private readonly SNIFContext _context;

        public ReportController(SNIFContext context)
        {
            _context = context;
        }

        private string GetUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");

        /// <summary>
        /// Submit a report against a user or pet.
        /// </summary>
        [HttpPost("reports")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubmitReport([FromBody] CreateReportDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var reporterId = GetUserId();

            if (reporterId == dto.TargetUserId)
                return BadRequest(new { message = "You cannot report yourself." });

            // Verify target user exists
            var targetExists = await _context.Users.AnyAsync(u => u.Id == dto.TargetUserId);
            if (!targetExists)
                return BadRequest(new { message = "Target user not found." });

            // Verify target pet exists if provided
            if (dto.TargetPetId != null)
            {
                var petExists = await _context.Pets.AnyAsync(p => p.Id == dto.TargetPetId);
                if (!petExists)
                    return BadRequest(new { message = "Target pet not found." });
            }

            var report = new Report
            {
                Id = Guid.NewGuid().ToString(),
                ReporterId = reporterId,
                TargetUserId = dto.TargetUserId,
                TargetPetId = dto.TargetPetId,
                Reason = dto.Reason,
                Description = dto.Description,
                Status = ReportStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Reports.Add(report);
            await _context.SaveChangesAsync();

            return CreatedAtAction(null, new { id = report.Id }, new { report.Id, report.Status });
        }

        /// <summary>
        /// Block a user.
        /// </summary>
        [HttpPost("users/{id}/block")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> BlockUser(string id)
        {
            var blockerId = GetUserId();

            if (blockerId == id)
                return BadRequest(new { message = "You cannot block yourself." });

            var targetExists = await _context.Users.AnyAsync(u => u.Id == id);
            if (!targetExists)
                return BadRequest(new { message = "User not found." });

            var alreadyBlocked = await _context.UserBlocks
                .AnyAsync(b => b.BlockerUserId == blockerId && b.BlockedUserId == id);

            if (alreadyBlocked)
                return Ok(new { message = "User is already blocked." });

            var block = new UserBlock
            {
                Id = Guid.NewGuid().ToString(),
                BlockerUserId = blockerId,
                BlockedUserId = id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserBlocks.Add(block);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User blocked." });
        }

        /// <summary>
        /// Unblock a user.
        /// </summary>
        [HttpDelete("users/{id}/block")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UnblockUser(string id)
        {
            var blockerId = GetUserId();

            var block = await _context.UserBlocks
                .FirstOrDefaultAsync(b => b.BlockerUserId == blockerId && b.BlockedUserId == id);

            if (block == null)
                return NotFound(new { message = "Block not found." });

            _context.UserBlocks.Remove(block);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User unblocked." });
        }
    }
}
