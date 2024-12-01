using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{
    public record LocationDto
    {
        [Required]
        [Range(-90, 90)]
        public double Latitude { get; init; }

        [Required]
        [Range(-180, 180)]
        public double Longitude { get; init; }

        public string? Address { get; init; }
        public string? City { get; init; }
        public string? Country { get; init; }
    }
}