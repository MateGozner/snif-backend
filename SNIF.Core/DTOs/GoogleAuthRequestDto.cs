using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{
    public record GoogleAuthRequestDto
    {
        [Required]
        public required string IdToken { get; init; }

        public LocationDto? Location { get; init; }
    }
}
