using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SNIF.Core.Enums;
using SNIF.Core.Models;

namespace SNIF.Core.Entities
{
    public class Pet : BaseEntity
    {
        public Pet()
        {
            Purpose = new List<PetPurpose>();
            Personality = new List<string>();
            Photos = new List<string>();
            Videos = new List<string>();
        }

        [Required]
        public string OwnerId { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = null!;

        [Required]
        [StringLength(50)]
        public string Species { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string Breed { get; set; } = null!;

        [Range(0, 50)]
        public int Age { get; set; }

        public Gender Gender { get; set; }

        public virtual ICollection<PetPurpose> Purpose { get; set; }

        public virtual ICollection<string> Personality { get; set; }

        public MedicalHistory? MedicalHistory { get; set; }

        public virtual ICollection<string> Photos { get; set; }

        public virtual ICollection<string> Videos { get; set; }

        public Location? Location { get; set; }

        // Navigation property
        public virtual User Owner { get; set; } = null!;
    }
}