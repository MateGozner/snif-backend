using System.Text;

namespace SNIF.Core.Configuration;

public static class JwtKeyValidator
{
    public const int MinimumKeyLengthBytes = 64;

    private static readonly HashSet<string> PlaceholderValues = new(StringComparer.Ordinal)
    {
        "dev-only-jwt-key-change-before-deploying-0123456789abcdef0123456789abcdef0123456789abcdef",
        "changeme",
        "change-me",
        "replace-me",
        "your-jwt-key-here",
        "your-secret-key-here",
        "development-only-jwt-key"
    };

    public static byte[] GetValidatedKeyBytes(string? configuredKey)
    {
        var normalizedKey = GetValidatedKey(configuredKey);
        return Encoding.UTF8.GetBytes(normalizedKey);
    }

    public static string GetValidatedKey(string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            throw new InvalidOperationException(
                "JWT signing key is missing. Configure Jwt:Key or Jwt__Key with a unique secret.");
        }

        var normalizedKey = configuredKey.Trim();
        if (PlaceholderValues.Contains(normalizedKey) || LooksLikePlaceholder(normalizedKey))
        {
            throw new InvalidOperationException(
                "JWT signing key is using a placeholder value. Configure Jwt:Key or Jwt__Key with a unique secret that is not committed to the repository.");
        }

        var keyBytes = Encoding.UTF8.GetBytes(normalizedKey);
        if (keyBytes.Length < MinimumKeyLengthBytes)
        {
            throw new InvalidOperationException(
                $"JWT signing key must be at least {MinimumKeyLengthBytes} bytes long.");
        }

        return normalizedKey;
    }

    private static bool LooksLikePlaceholder(string key)
    {
        var normalized = key.Trim().ToLowerInvariant();

        return normalized.Contains("change-before-deploy", StringComparison.Ordinal)
            || normalized.Contains("change-before-production", StringComparison.Ordinal)
            || normalized.Contains("dev-only", StringComparison.Ordinal)
            || normalized.Contains("replace-this", StringComparison.Ordinal)
            || normalized.Contains("replace_me", StringComparison.Ordinal)
            || normalized.Contains("your-secret", StringComparison.Ordinal);
    }
}