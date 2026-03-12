using System.ComponentModel.DataAnnotations;
using SNIF.Core.Entities;

namespace SNIF.Core.Models
{
    public class DiscoveryPreferences : BaseModel
    {
        [Required]
        public string PetId { get; set; } = null!;
        public virtual Pet Pet { get; set; } = null!;

        public bool AllowOtherBreeds { get; set; } = true;

        public bool AllowOtherSpecies { get; set; } = false;

        [Range(0, 50)]
        public int? MinAge { get; set; }

        [Range(0, 50)]
        public int? MaxAge { get; set; }

        public string? PreferredGender { get; set; }

        public string? PreferredPurposes { get; set; }
    }
}
