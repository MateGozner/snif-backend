using System.Text.Json;
using System.Text.Json.Serialization;

namespace SNIF.Core.DTOs
{
    // === Checkout ===
    public class LsCheckoutRequest
    {
        [JsonPropertyName("data")]
        public LsCheckoutData Data { get; set; } = new();
    }

    public class LsCheckoutData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "checkouts";

        [JsonPropertyName("attributes")]
        public LsCheckoutAttributes Attributes { get; set; } = new();

        [JsonPropertyName("relationships")]
        public LsCheckoutRelationships Relationships { get; set; } = new();
    }

    public class LsCheckoutAttributes
    {
        [JsonPropertyName("checkout_data")]
        public LsCheckoutCustomData CheckoutData { get; set; } = new();

        [JsonPropertyName("product_options")]
        public LsCheckoutProductOptions ProductOptions { get; set; } = new();
    }

    public class LsCheckoutProductOptions
    {
        /// <summary>
        /// Restricts the checkout to only display the specified variant IDs.
        /// When set, the user cannot switch to other variants.
        /// </summary>
        [JsonPropertyName("enabled_variants")]
        public List<int> EnabledVariants { get; set; } = new();

        /// <summary>
        /// URL to redirect the customer to after a successful purchase.
        /// </summary>
        [JsonPropertyName("redirect_url")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RedirectUrl { get; set; }

        /// <summary>
        /// Text shown on the receipt button that links back to the app.
        /// </summary>
        [JsonPropertyName("receipt_button_text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ReceiptButtonText { get; set; }
    }

    public class LsCheckoutCustomData
    {
        [JsonPropertyName("email")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Name { get; set; }

        [JsonPropertyName("custom")]
        public Dictionary<string, string> Custom { get; set; } = new();
    }

    public class LsCheckoutRelationships
    {
        [JsonPropertyName("store")]
        public LsRelationship Store { get; set; } = new();

        [JsonPropertyName("variant")]
        public LsRelationship Variant { get; set; } = new();
    }

    public class LsRelationship
    {
        [JsonPropertyName("data")]
        public LsRelationshipData Data { get; set; } = new();
    }

    public class LsRelationshipData
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    // === Checkout Response ===
    public class LsCheckoutResponse
    {
        [JsonPropertyName("data")]
        public LsCheckoutResponseData Data { get; set; } = new();
    }

    public class LsCheckoutResponseData
    {
        [JsonPropertyName("attributes")]
        public LsCheckoutResponseAttributes Attributes { get; set; } = new();
    }

    public class LsCheckoutResponseAttributes
    {
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    // === Subscription ===
    public class LsSubscriptionResponse
    {
        [JsonPropertyName("data")]
        public LsSubscriptionData Data { get; set; } = new();
    }

    public class LsSubscriptionData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("attributes")]
        public LsSubscriptionAttributes Attributes { get; set; } = new();
    }

    public class LsSubscriptionAttributes
    {
        [JsonPropertyName("customer_id")]
        public int? CustomerId { get; set; }

        [JsonPropertyName("order_id")]
        public int? OrderId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("variant_id")]
        public int VariantId { get; set; }

        [JsonPropertyName("user_name")]
        public string? UserName { get; set; }

        [JsonPropertyName("user_email")]
        public string? UserEmail { get; set; }

        [JsonPropertyName("renews_at")]
        public DateTime? RenewsAt { get; set; }

        [JsonPropertyName("ends_at")]
        public DateTime? EndsAt { get; set; }

        [JsonPropertyName("cancelled")]
        public bool Cancelled { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class LsSubscriptionListResponse
    {
        [JsonPropertyName("data")]
        public List<LsSubscriptionData> Data { get; set; } = new();
    }

    public class LsOrderListResponse
    {
        [JsonPropertyName("data")]
        public List<LsOrderData> Data { get; set; } = new();
    }

    public class LsOrderData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("attributes")]
        public LsOrderAttributes Attributes { get; set; } = new();
    }

    public class LsOrderAttributes
    {
        [JsonPropertyName("customer_id")]
        public int? CustomerId { get; set; }

        [JsonPropertyName("order_number")]
        public int? OrderNumber { get; set; }

        [JsonPropertyName("user_email")]
        public string? UserEmail { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("first_order_item")]
        public LsOrderItemAttributes? FirstOrderItem { get; set; }
    }

    public class LsOrderItemAttributes
    {
        [JsonPropertyName("variant_id")]
        public int VariantId { get; set; }
    }

    // === Webhook Payload ===
    public class LsWebhookPayload
    {
        [JsonPropertyName("meta")]
        public LsWebhookMeta Meta { get; set; } = new();

        [JsonPropertyName("data")]
        public LsWebhookData Data { get; set; } = new();
    }

    public class LsWebhookMeta
    {
        [JsonPropertyName("event_name")]
        public string EventName { get; set; } = string.Empty;

        [JsonPropertyName("custom_data")]
        public JsonElement? CustomData { get; set; }
    }

    public class LsWebhookData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("attributes")]
        public LsWebhookAttributes Attributes { get; set; } = new();
    }

    public class LsWebhookAttributes
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("variant_id")]
        public JsonElement? VariantId { get; set; }

        [JsonPropertyName("subscription_id")]
        public JsonElement? SubscriptionId { get; set; }

        [JsonPropertyName("first_subscription_item")]
        public LsFirstSubscriptionItem? FirstSubscriptionItem { get; set; }

        [JsonPropertyName("renews_at")]
        public DateTime? RenewsAt { get; set; }

        [JsonPropertyName("ends_at")]
        public DateTime? EndsAt { get; set; }

        [JsonPropertyName("cancelled")]
        public bool? Cancelled { get; set; }

        [JsonPropertyName("customer_id")]
        public JsonElement? CustomerId { get; set; }

        [JsonPropertyName("order_number")]
        public JsonElement? OrderNumber { get; set; }

        [JsonPropertyName("total")]
        public JsonElement? Total { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("updated_at")]
        public DateTime? UpdatedAt { get; set; }
    }

    public class LsFirstSubscriptionItem
    {
        [JsonPropertyName("id")]
        public JsonElement? Id { get; set; }

        [JsonPropertyName("subscription_id")]
        public JsonElement? SubscriptionId { get; set; }

        [JsonPropertyName("price_id")]
        public JsonElement? PriceId { get; set; }
    }
}
