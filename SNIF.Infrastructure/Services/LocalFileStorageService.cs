using Microsoft.AspNetCore.Hosting;
using SNIF.Core.Interfaces;

namespace SNIF.Infrastructure.Services;

public class LocalFileStorageService : IMediaStorageService
{
    private readonly string _uploadsPath;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        _uploadsPath = Path.Combine(webRoot, "uploads");
        Directory.CreateDirectory(_uploadsPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        var uniqueName = $"{Guid.NewGuid()}-{fileName}";
        var filePath = Path.Combine(_uploadsPath, uniqueName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        using var fs = new FileStream(filePath, FileMode.Create);
        await fileStream.CopyToAsync(fs);

        return $"/uploads/{uniqueName}";
    }

    public Task<bool> DeleteAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl))
            return Task.FromResult(false);

        // fileUrl is a relative path like /uploads/xxx-file.jpg
        var relativePath = fileUrl.TrimStart('/');
        var webRoot = Path.GetDirectoryName(_uploadsPath)!; // parent of uploads
        var fullPath = Path.Combine(webRoot, relativePath);

        // Prevent path traversal
        var resolvedPath = Path.GetFullPath(fullPath);
        if (!resolvedPath.StartsWith(Path.GetFullPath(_uploadsPath)))
            return Task.FromResult(false);

        if (File.Exists(resolvedPath))
        {
            File.Delete(resolvedPath);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<string> GetPresignedUrlAsync(string fileUrl, TimeSpan expiry)
    {
        // Local storage doesn't need presigned URLs; return the URL as-is
        return Task.FromResult(fileUrl);
    }
}
