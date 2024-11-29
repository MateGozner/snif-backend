namespace SNIF.Core.DTOs
{
    public record AuthResponseDto
    {
        public string Email { get; init; } = null!;
        public string Token { get; init; } = null!;
        public string Name { get; init; } = null!;
    }
}