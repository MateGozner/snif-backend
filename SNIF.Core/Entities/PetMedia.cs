using SNIF.Core.DTOs;

namespace SNIF.Core.Entities
{
    public class PetMedia : BaseEntity
    {
        public string FileName { get; set; } = null!;
        public string ContentType { get; set; } = null!;
        public long Size { get; set; }
        public MediaType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }

        // Navigation properties
        public string PetId { get; set; } = null!;
        public virtual Pet Pet { get; set; } = null!;
    }
}