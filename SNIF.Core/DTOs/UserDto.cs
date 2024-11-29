using SNIF.Core.Models;

namespace SNIF.Core.DTOs
{
    public record UserDto
    {
        public string Id { get; init; } = null!;
        public string Email { get; init; } = null!;
        public string Name { get; init; } = null!;
        public Location Location { get; init; } = null!;
    }

    public record CreateUserDto
    {
        public required string Email { get; init; }
        public required string Password { get; init; }
        public required string Name { get; init; }
    }

    public record UpdateUserDto
    {
        public string? Name { get; init; }
        public Location Location { get; init; } = null!;
    }

    public record LoginDto
    {
        public required string Email { get; init; }
        public required string Password { get; init; }
    }


}