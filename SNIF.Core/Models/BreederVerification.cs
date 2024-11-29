using SNIF.Core.Entities;

namespace SNIF.Core.Models
{
    public class BreederVerification : BaseModel
    {
        public BreederVerification()
        {
            Documents = new List<string>();
        }

        public bool IsVerified { get; set; }
        public virtual ICollection<string> Documents { get; set; }
        public string? VerificationNotes { get; set; }
        public DateTime? VerifiedAt { get; set; }
        
        // Navigation properties
        public string UserId { get; set; } = null!;
        public virtual User User { get; set; } = null!;
    }
}