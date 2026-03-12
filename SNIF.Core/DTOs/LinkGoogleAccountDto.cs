using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{
    public record LinkGoogleAccountDto
    {
        [Required]
        public required string IdToken { get; init; }
    }
}
