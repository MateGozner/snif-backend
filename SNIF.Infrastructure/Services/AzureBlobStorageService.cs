using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Configuration;
using SNIF.Core.Interfaces;

namespace SNIF.Infrastructure.Services;

public class AzureBlobStorageService : IMediaStorageService
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStorageService(IConfiguration configuration)
    {
        var connectionString = configuration["Storage:ConnectionString"]
            ?? throw new InvalidOperationException("Storage:ConnectionString is not configured.");
        var containerName = configuration["Storage:ContainerName"] ?? "pet-media";

        var serviceClient = new BlobServiceClient(connectionString);
        _containerClient = serviceClient.GetBlobContainerClient(containerName);
        // NOTE: PublicAccessType.Blob allows direct read access to individual blobs via URL
        // (container listing remains private). If the container already exists in Azure,
        // this call won't change its access level — update it via Azure Portal or CLI:
        //   az storage container set-permission --name pet-media --public-access blob
        _containerClient.CreateIfNotExists(PublicAccessType.Blob);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        var blobName = $"{Guid.NewGuid()}-{fileName}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(fileStream, new BlobUploadOptions { HttpHeaders = headers });

        return blobClient.Uri.ToString();
    }

    public async Task<bool> DeleteAsync(string fileUrl)
    {
        var blobName = ExtractBlobName(fileUrl);
        if (string.IsNullOrEmpty(blobName))
            return false;

        var blobClient = _containerClient.GetBlobClient(blobName);
        var response = await blobClient.DeleteIfExistsAsync();
        return response.Value;
    }

    public Task<string> GetPresignedUrlAsync(string fileUrl, TimeSpan expiry)
    {
        var blobName = ExtractBlobName(fileUrl);
        if (string.IsNullOrEmpty(blobName))
            throw new ArgumentException("Invalid blob URL.", nameof(fileUrl));

        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!blobClient.CanGenerateSasUri)
            throw new InvalidOperationException("BlobClient is not authorized to generate SAS tokens. Use a connection string with account key.");

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _containerClient.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);
        return Task.FromResult(sasUri.ToString());
    }

    private string? ExtractBlobName(string fileUrl)
    {
        if (!Uri.TryCreate(fileUrl, UriKind.Absolute, out var uri))
            return null;

        // Blob URL format: https://<account>.blob.core.windows.net/<container>/<blobName>
        var segments = uri.Segments;
        if (segments.Length < 3)
            return null;

        // Skip the leading "/" and container segments, join the rest
        return string.Join("", segments.Skip(2)).TrimStart('/');
    }
}
