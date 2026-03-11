using System.ComponentModel.DataAnnotations;
using SNIF.Core.Models;

namespace SNIF.Core.Entities
{
    public class AnimalBreed : BaseEntity
    {
        [Required]
        [StringLength(50)]
        public string Species { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        public bool IsCustom { get; set; } = false;
    }
}
