using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SNIF.Core.DTOs
{
    public record DiscoveryPreferencesDto
    {
        public int Id { get; init; }
        public string PetId { get; init; } = null!;
        public bool AllowOtherBreeds { get; init; } = true;
        public bool AllowOtherSpecies { get; init; }
        public int? MinAge { get; init; }
        public int? MaxAge { get; init; }
        public string? PreferredGender { get; init; }
        public List<string>? PreferredPurposes { get; init; }
    }

    public record UpdateDiscoveryPreferencesDto
    {
        public bool AllowOtherBreeds { get; init; } = true;
        public bool AllowOtherSpecies { get; init; }

        [Range(0, 50)]
        public int? MinAge { get; init; }

        [Range(0, 50)]
        public int? MaxAge { get; init; }

        public string? PreferredGender { get; init; }
        public List<string>? PreferredPurposes { get; init; }
    }
}
