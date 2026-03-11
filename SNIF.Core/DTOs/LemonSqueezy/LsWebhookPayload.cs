using System.Text.Json.Serialization;

namespace SNIF.Core.DTOs.LemonSqueezy;

public class LsWebhookPayload
{
    public LsWebhookMeta Meta { get; set; } = new();
    public LsWebhookData Data { get; set; } = new();
}

public class LsWebhookMeta
{
    [JsonPropertyName("event_name")]
    public string EventName { get; set; } = "";
    
    [JsonPropertyName("custom_data")]
    public Dictionary<string, string>? CustomData { get; set; }
}

public class LsWebhookData
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public LsSubscriptionAttributes Attributes { get; set; } = new();
}
