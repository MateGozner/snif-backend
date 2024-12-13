
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SNIF.Core.Enums;

namespace SNIF.Core.DTOs
{
    public record PetDto
    {
        public string Id { get; init; } = null!;
        public string Name { get; init; } = null!;
        public string Species { get; init; } = null!;
        public string Breed { get; init; } = null!;
        public int Age { get; init; }
        public Gender Gender { get; init; }
        public ICollection<PetPurpose> Purpose { get; init; } = new List<PetPurpose>();
        public ICollection<string> Personality { get; init; } = new List<string>();
        public MedicalHistoryDto? MedicalHistory { get; init; }
        public ICollection<string> Photos { get; init; } = new List<string>();
        public ICollection<string> Videos { get; init; } = new List<string>();
        public LocationDto? Location { get; init; }
        public string OwnerId { get; init; } = null!;
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }

    public record CreatePetDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; init; } = null!;

        [Required]
        [StringLength(50)]
        public string Species { get; init; } = null!;

        [Required]
        [StringLength(100)]
        public string Breed { get; init; } = null!;

        [Range(0, 50)]
        public int Age { get; init; }

        public Gender Gender { get; init; }
        public ICollection<PetPurpose> Purpose { get; init; } = new List<PetPurpose>();
        public ICollection<string> Personality { get; init; } = new List<string>();
        public CreateMedicalHistoryDto? MedicalHistory { get; init; }
        public LocationDto? Location { get; init; }

        public IFormFileCollection? Photos { get; init; }
        public IFormFileCollection? Videos { get; init; }
    }

    public record UpdatePetDto
    {
        [StringLength(100)]
        public string? Name { get; init; }

        [StringLength(50)]
        public string? Species { get; init; }

        [StringLength(100)]
        public string? Breed { get; init; }

        [Range(0, 50)]
        public int? Age { get; init; }

        public Gender? Gender { get; init; }
        public ICollection<PetPurpose>? Purpose { get; init; }
        public ICollection<string>? Personality { get; init; }
        public UpdateMedicalHistoryDto? MedicalHistory { get; init; }
        public LocationDto? Location { get; init; }
    }

    public record MedicalHistoryDto
    {
        public bool IsVaccinated { get; init; }
        public ICollection<string> HealthIssues { get; init; } = new List<string>();
        public ICollection<string> VaccinationRecords { get; init; } = new List<string>();
        public DateTime? LastCheckup { get; init; }
        public string? VetContact { get; init; }
    }

    public record CreateMedicalHistoryDto
    {
        public bool IsVaccinated { get; init; }
        public ICollection<string> HealthIssues { get; init; } = new List<string>();
        public ICollection<string> VaccinationRecords { get; init; } = new List<string>();
        public DateTime? LastCheckup { get; init; }
        public string? VetContact { get; init; }
    }

    public record UpdateMedicalHistoryDto
    {
        public bool? IsVaccinated { get; init; }
        public ICollection<string>? HealthIssues { get; init; }
        public ICollection<string>? VaccinationRecords { get; init; }
        public DateTime? LastCheckup { get; init; }
        public string? VetContact { get; init; }
    }
}