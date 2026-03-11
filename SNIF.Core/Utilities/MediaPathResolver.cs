using SNIF.Core.DTOs;

namespace SNIF.Core.Utilities;

public static class MediaPathResolver
{
    public static string? ResolveProfilePicturePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return null;

        if (IsDirectPath(storedPath))
            return storedPath;

        return $"/uploads/profiles/{Path.GetFileName(storedPath)}";
    }

    public static string? ResolvePetMediaPath(string? storedPath, MediaType type)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return null;

        if (IsDirectPath(storedPath))
            return storedPath;

        var folder = type == MediaType.Photo ? "photos" : "videos";
        return $"/uploads/pets/{folder}/{Path.GetFileName(storedPath)}";
    }

    private static bool IsDirectPath(string storedPath)
    {
        return storedPath.StartsWith("/", StringComparison.Ordinal)
            || Uri.TryCreate(storedPath, UriKind.Absolute, out _);
    }
}