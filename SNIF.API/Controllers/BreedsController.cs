using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SNIF.Core.Entities;
using SNIF.Infrastructure.Data;

namespace SNIF.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requires user to be logged in
    [EnableRateLimiting("global")]
    public class BreedsController : ControllerBase
    {
        private readonly SNIFContext _context;

        public BreedsController(SNIFContext context)
        {
            _context = context;
        }

        // GET: api/breeds?species=Dog
        [HttpGet]
        public async Task<IActionResult> GetBreedsBySpecies([FromQuery] string species)
        {
            if (string.IsNullOrWhiteSpace(species))
            {
                return BadRequest("Species query parameter is required.");
            }

            var breeds = await _context.AnimalBreeds
                .Where(b => b.Species.ToLower() == species.ToLower())
                .OrderBy(b => b.Name)
                .Select(b => new { b.Id, b.Species, b.Name, b.IsCustom })
                .ToListAsync();

            return Ok(breeds);
        }

        // POST: api/breeds
        [HttpPost]
        public async Task<IActionResult> CreateCustomBreed([FromBody] CreateBreedRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Species) || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest("Species and Name are required.");
            }

            // Check if it already exists (case-insensitive) to prevent duplicates
            var exists = await _context.AnimalBreeds.AnyAsync(b =>
                b.Species.ToLower() == request.Species.ToLower() &&
                b.Name.ToLower() == request.Name.ToLower());

            if (exists)
            {
                return Conflict(new { message = "This breed already exists for the given species." });
            }

            var newBreed = new AnimalBreed
            {
                Id = Guid.NewGuid().ToString(),
                Species = request.Species.Trim(),
                Name = request.Name.Trim(),
                IsCustom = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.AnimalBreeds.Add(newBreed);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBreedsBySpecies), new { species = newBreed.Species }, new
            {
                newBreed.Id,
                newBreed.Species,
                newBreed.Name,
                newBreed.IsCustom
            });
        }
    }

    public class CreateBreedRequest
    {
        public string Species { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
