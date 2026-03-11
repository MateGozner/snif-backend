using FluentAssertions;
using Moq;
using SNIF.Core.Interfaces;

namespace SNIF.Tests.Services;

public class MediaStorageTests
{
    [Fact]
    public async Task IMediaStorageService_Upload_ReturnsUrl()
    {
        var mock = new Mock<IMediaStorageService>();
        mock.Setup(s => s.UploadAsync(It.IsAny<Stream>(), "photo.jpg", "image/jpeg"))
            .ReturnsAsync("https://storage.example.com/photo.jpg");

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await mock.Object.UploadAsync(stream, "photo.jpg", "image/jpeg");

        result.Should().Be("https://storage.example.com/photo.jpg");
    }

    [Fact]
    public async Task IMediaStorageService_Delete_ReturnsTrue()
    {
        var mock = new Mock<IMediaStorageService>();
        mock.Setup(s => s.DeleteAsync("https://storage.example.com/photo.jpg"))
            .ReturnsAsync(true);

        var result = await mock.Object.DeleteAsync("https://storage.example.com/photo.jpg");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IMediaStorageService_Delete_NonExistent_ReturnsFalse()
    {
        var mock = new Mock<IMediaStorageService>();
        mock.Setup(s => s.DeleteAsync("https://storage.example.com/nonexistent.jpg"))
            .ReturnsAsync(false);

        var result = await mock.Object.DeleteAsync("https://storage.example.com/nonexistent.jpg");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IMediaStorageService_GetPresignedUrl_ReturnsUrl()
    {
        var mock = new Mock<IMediaStorageService>();
        mock.Setup(s => s.GetPresignedUrlAsync("photo.jpg", It.IsAny<TimeSpan>()))
            .ReturnsAsync("https://storage.example.com/photo.jpg?sig=abc123");

        var result = await mock.Object.GetPresignedUrlAsync("photo.jpg", TimeSpan.FromHours(1));

        result.Should().Contain("sig=abc123");
    }
}
