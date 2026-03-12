using System.Text.Json.Serialization;

namespace SNIF.Core.DTOs.LemonSqueezy;

public class LsSubscriptionAttributes
{
    [JsonPropertyName("store_id")]
    public int StoreId { get; set; }
    
    [JsonPropertyName("customer_id")]
    public int CustomerId { get; set; }
    
    [JsonPropertyName("variant_id")]
    public int VariantId { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
    
    [JsonPropertyName("renews_at")]
    public DateTime? RenewsAt { get; set; }
    
    [JsonPropertyName("ends_at")]
    public DateTime? EndsAt { get; set; }
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
