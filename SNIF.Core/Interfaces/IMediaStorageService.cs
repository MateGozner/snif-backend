namespace SNIF.Core.Interfaces;

public interface IMediaStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType);
    Task<bool> DeleteAsync(string fileUrl);
    Task<string> GetPresignedUrlAsync(string fileUrl, TimeSpan expiry);
}
