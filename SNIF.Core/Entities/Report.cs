using SNIF.Core.Enums;

namespace SNIF.Core.Entities
{
    public class Report : BaseEntity
    {
        public string ReporterId { get; set; } = null!;
        public virtual User Reporter { get; set; } = null!;

        public string TargetUserId { get; set; } = null!;
        public virtual User TargetUser { get; set; } = null!;

        public string? TargetPetId { get; set; }
        public virtual Pet? TargetPet { get; set; }

        public ReportReason Reason { get; set; }
        public string? Description { get; set; }

        public ReportStatus Status { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedBy { get; set; }
        public string? Resolution { get; set; }
        public string? Notes { get; set; }
    }
}
