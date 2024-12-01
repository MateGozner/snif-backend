
namespace SNIF.Core.DTOs
{
    public record PetDto
    {
        public string Id { get; init; } = null!;
        public string Name { get; init; } = null!;
        public string Species { get; init; } = null!;
        public string Breed { get; init; } = null!;
    }
}