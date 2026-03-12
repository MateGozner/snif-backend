using System.ComponentModel.DataAnnotations;
using SNIF.Core.Enums;

namespace SNIF.Core.DTOs
{
    public record CreateReportDto
    {
        [Required]
        public string TargetUserId { get; init; } = null!;

        public string? TargetPetId { get; init; }

        [Required]
        public ReportReason Reason { get; init; }

        [MaxLength(1000)]
        public string? Description { get; init; }
    }
}
