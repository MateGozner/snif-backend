using System.ComponentModel.DataAnnotations;
using SNIF.Core.Models;

namespace SNIF.Core.DTOs
{
    public record UserDto
    {
        public string Id { get; init; } = null!;
        public string Email { get; init; } = null!;
        public string Name { get; init; } = null!;
        public LocationDto? Location { get; init; }
        public ICollection<PetDto> Pets { get; init; } = new List<PetDto>();
        public BreederVerification? BreederVerification { get; init; }
        public UserPreferences? Preferences { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    public record CreateUserDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public required string Password { get; init; }

        [Required]
        [StringLength(50)]
        public required string Name { get; init; }

        public LocationDto? Location { get; init; }
    }

    public record UpdateUserDto
    {
        [StringLength(50)]
        public string? Name { get; init; }
        public LocationDto? Location { get; init; }
        public UserPreferences? Preferences { get; init; }
        public BreederVerification? BreederVerification { get; init; }
    }

    public record LoginDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; init; }

        [Required]
        public required string Password { get; init; }

        public bool RememberMe { get; init; }

        public LocationDto? Location { get; init; }
    }


}