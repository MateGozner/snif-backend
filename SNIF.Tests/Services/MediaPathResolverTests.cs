using FluentAssertions;
using SNIF.Core.DTOs;
using SNIF.Core.Utilities;

namespace SNIF.Tests.Services;

public class MediaPathResolverTests
{
    [Theory]
    [InlineData("avatar.jpg", "/uploads/profiles/avatar.jpg")]
    [InlineData("/uploads/profiles/avatar.jpg", "/uploads/profiles/avatar.jpg")]
    [InlineData("https://cdn.example.com/avatar.jpg", "https://cdn.example.com/avatar.jpg")]
    public void ResolveProfilePicturePath_ReturnsCanonicalPath(string storedPath, string expected)
    {
        var result = MediaPathResolver.ResolveProfilePicturePath(storedPath);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("pet-photo.jpg", MediaType.Photo, "/uploads/pets/photos/pet-photo.jpg")]
    [InlineData("pet-video.mp4", MediaType.Video, "/uploads/pets/videos/pet-video.mp4")]
    [InlineData("/uploads/pets/photos/pet-photo.jpg", MediaType.Photo, "/uploads/pets/photos/pet-photo.jpg")]
    [InlineData("https://cdn.example.com/pets/pet-photo.jpg", MediaType.Photo, "https://cdn.example.com/pets/pet-photo.jpg")]
    public void ResolvePetMediaPath_ReturnsCanonicalPath(string storedPath, MediaType type, string expected)
    {
        var result = MediaPathResolver.ResolvePetMediaPath(storedPath, type);

        result.Should().Be(expected);
    }
}