using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
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
        public PreferencesDto? Preferences { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
        public string? ProfilePicturePath { get; init; }
        public bool IsOnline { get; init; }
        public DateTime? LastSeen { get; init; }
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

    public class UpdateUserDto
    {
        public string Name { get; set; }
    }

    public class ProfilePictureResponseDto
    {
        public string Url { get; set; }
    }


    public class UpdateProfilePictureDto
    {
        public required string FileName { get; set; }
        public required string ContentType { get; set; }
        public required string Base64Data { get; set; }
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