using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{
    public record ConfirmEmailDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; init; }

        [Required]
        public required string Token { get; init; }
    }

    public record ResendConfirmationDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; init; }
    }
}
