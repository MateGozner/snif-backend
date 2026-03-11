using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{
    public record ForgotPasswordDto
    {
        [Required]
        [EmailAddress]
        public required string Email { get; init; }
    }

    public record ResetPasswordDto
    {
        [Required]
        public required string Token { get; init; }

        [Required]
        [EmailAddress]
        public required string Email { get; init; }

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public required string NewPassword { get; init; }

        [Required]
        [Compare(nameof(NewPassword))]
        public required string ConfirmPassword { get; init; }
    }
}
