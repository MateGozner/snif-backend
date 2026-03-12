namespace SNIF.Core.Configuration;

public static class GoogleClientIdValidator
{
    public static IReadOnlyList<string> GetValidatedClientIds(
        string? webClientId,
        string? iosClientId,
        string? androidClientId,
        bool requireAtLeastOneClientId)
    {
        var clientIds = new[]
        {
            webClientId,
            iosClientId,
            androidClientId
        }
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Select(value => value!.Trim())
        .Distinct(StringComparer.Ordinal)
        .ToArray();

        if (requireAtLeastOneClientId && clientIds.Length == 0)
        {
            throw new InvalidOperationException(
                "Google OAuth client IDs are missing. Configure at least one of Google:ClientId, Google:ClientIdIos, or Google:ClientIdAndroid before starting outside tests.");
        }

        return clientIds;
    }
}