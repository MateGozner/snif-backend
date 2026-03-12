using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{
    public record SetPasswordDto
    {
        [Required]
        [StringLength(100, MinimumLength = 8)]
        public required string NewPassword { get; init; }

        [Required]
        [Compare("NewPassword")]
        public required string ConfirmPassword { get; init; }
    }
}
