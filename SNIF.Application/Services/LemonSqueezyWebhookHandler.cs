using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SNIF.Core.Configuration;
using SNIF.Core.DTOs;
using SNIF.Core.Enums;
using SNIF.Core.Interfaces;
using SNIF.Infrastructure.Data;

namespace SNIF.Busniess.Services
{
    public class LemonSqueezyWebhookHandler
    {
        private static readonly JsonSerializerOptions WebhookSerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ISubscriptionService _subscriptionService;
        private readonly SNIFContext _context;
        private readonly LemonSqueezyOptions _options;
        private readonly ILogger<LemonSqueezyWebhookHandler> _logger;
        private static readonly ConcurrentDictionary<string, DateTime> _processedEvents = new();

        public LemonSqueezyWebhookHandler(
            ISubscriptionService subscriptionService,
            SNIFContext context,
            IOptions<LemonSqueezyOptions> options,
            ILogger<LemonSqueezyWebhookHandler> logger)
        {
            _subscriptionService = subscriptionService;
            _context = context;
            _options = options.Value;
            _logger = logger;
        }

        public async Task HandleAsync(string json)
        {
            LsWebhookPayload payload;
            try
            {
                using var document = JsonDocument.Parse(json);
                payload = document.RootElement.Deserialize<LsWebhookPayload>(WebhookSerializerOptions)
                    ?? throw new InvalidOperationException("Failed to deserialize webhook payload.");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize LS webhook payload. Body snippet: {BodySnippet}",
                    Truncate(json, 512));
                throw new InvalidOperationException($"Failed to deserialize webhook payload: {ex.Message}", ex);
            }

            var eventKey = $"{payload.Data.Id}:{payload.Meta.EventName}";

            // Idempotency: skip already-processed events
            if (!_processedEvents.TryAdd(eventKey, DateTime.UtcNow))
            {
                _logger.LogInformation("Skipping duplicate LS event {EventKey}", eventKey);
                return;
            }

            // Evict entries older than 24h
            var cutoff = DateTime.UtcNow.AddHours(-24);
            foreach (var key in _processedEvents.Keys)
            {
                if (_processedEvents.TryGetValue(key, out var ts) && ts < cutoff)
                    _processedEvents.TryRemove(key, out _);
            }

            _logger.LogInformation("Processing LS event {EventName} for data ID {DataId}",
                payload.Meta.EventName, payload.Data.Id);

            switch (payload.Meta.EventName)
            {
                case "subscription_created":
                    await HandleSubscriptionCreated(payload);
                    break;
                case "subscription_updated":
                    await HandleSubscriptionUpdated(payload);
                    break;
                case "subscription_cancelled":
                    await HandleSubscriptionCancelled(payload);
                    break;
                case "subscription_payment_failed":
                    await HandlePaymentFailed(payload);
                    break;
                case "subscription_payment_success":
                    await HandlePaymentSucceeded(payload);
                    break;
                case "order_created":
                    await HandleOrderCreated(payload);
                    break;
                default:
                    _logger.LogInformation("Ignoring unsupported LS event {EventName}", payload.Meta.EventName);
                    break;
            }
        }

        private async Task HandleSubscriptionCreated(LsWebhookPayload payload)
        {
            var userId = GetCustomDataValue(payload, "user_id");
            var planString = GetCustomDataValue(payload, "plan");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(planString))
            {
                _logger.LogWarning("subscription_created missing user_id or plan in custom_data");
                return;
            }

            if (!Enum.TryParse<SubscriptionPlan>(planString, out var plan))
            {
                _logger.LogWarning("Invalid plan value: {Plan}", planString);
                return;
            }

            var subscriptionId = payload.Data.Id;
            var customerId = GetAttributeValue(payload.Data.Attributes.CustomerId);

            await _subscriptionService.CreateOrUpdateSubscription(
                userId, plan, subscriptionId, customerId);

            // Update period dates if available
            if (payload.Data.Attributes.RenewsAt.HasValue)
            {
                await _subscriptionService.HandleSubscriptionUpdated(
                    subscriptionId,
                    SubscriptionStatus.Active,
                    DateTime.UtcNow,
                    payload.Data.Attributes.RenewsAt.Value,
                    false);
            }
        }

        private async Task HandleSubscriptionUpdated(LsWebhookPayload payload)
        {
            var subscriptionId = payload.Data.Id;
            var attrs = payload.Data.Attributes;

            var status = MapLsStatus(attrs.Status);
            var periodEnd = attrs.RenewsAt ?? attrs.EndsAt ?? DateTime.UtcNow.AddMonths(1);
            var cancelAtPeriodEnd = attrs.Cancelled ?? false;

            // Determine if plan changed by checking variant_id against known variants
            var planString = GetCustomDataValue(payload, "plan");
            if (!string.IsNullOrEmpty(planString) && Enum.TryParse<SubscriptionPlan>(planString, out var newPlan))
            {
                var userId = GetCustomDataValue(payload, "user_id");
                if (!string.IsNullOrEmpty(userId))
                {
                    var customerId = GetAttributeValue(attrs.CustomerId);
                    await _subscriptionService.CreateOrUpdateSubscription(
                        userId, newPlan, subscriptionId, customerId);
                }
            }

            await _subscriptionService.HandleSubscriptionUpdated(
                subscriptionId, status, DateTime.UtcNow, periodEnd, cancelAtPeriodEnd);
        }

        private async Task HandleSubscriptionCancelled(LsWebhookPayload payload)
        {
            var subscriptionId = payload.Data.Id;
            var attrs = payload.Data.Attributes;

            var periodEnd = attrs.EndsAt ?? attrs.RenewsAt ?? DateTime.UtcNow;

            await _subscriptionService.HandleSubscriptionUpdated(
                subscriptionId,
                SubscriptionStatus.Canceled,
                DateTime.UtcNow,
                periodEnd,
                true);
        }

        private async Task HandlePaymentSucceeded(LsWebhookPayload payload)
        {
            var subscriptionId = ResolveSubscriptionProviderId(payload);
            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                _logger.LogWarning(
                    "subscription_payment_success missing subscription reference for data ID {DataId}",
                    payload.Data.Id);
                return;
            }

            var subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.PaymentProviderSubscriptionId == subscriptionId);

            if (subscription == null)
            {
                _logger.LogWarning(
                    "subscription_payment_success received for unknown subscription {SubscriptionId}",
                    subscriptionId);
                return;
            }

            subscription.Status = SubscriptionStatus.Active;
            var customerId = GetAttributeValue(payload.Data.Attributes.CustomerId);
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                subscription.PaymentProviderCustomerId = customerId;
            }

            subscription.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        private async Task HandlePaymentFailed(LsWebhookPayload payload)
        {
            var subscriptionId = ResolveSubscriptionProviderId(payload) ?? payload.Data.Id;

            var sub = await _context.Subscriptions
                .FirstOrDefaultAsync(s => s.PaymentProviderSubscriptionId == subscriptionId);

            if (sub != null)
            {
                sub.Status = SubscriptionStatus.PastDue;
                sub.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private async Task HandleOrderCreated(LsWebhookPayload payload)
        {
            var userId = GetCustomDataValue(payload, "user_id");
            var type = GetCustomDataValue(payload, "type");
            var claimedAmount = GetCustomDataInt32(payload, "amount");

            if (type != "credit_purchase" || string.IsNullOrEmpty(userId) ||
                !claimedAmount.HasValue || claimedAmount.Value <= 0)
                return;

            // Validate variant_id → credit amount mapping (defense-in-depth)
            var validatedAmount = ResolveValidatedCreditAmount(payload, claimedAmount.Value);

            var balance = await _context.CreditBalances
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (balance == null)
            {
                balance = new Core.Entities.CreditBalance
                {
                    Id = Guid.NewGuid().ToString(),
                    UserId = userId,
                    Credits = validatedAmount,
                    LastPurchasedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.CreditBalances.Add(balance);
            }
            else
            {
                balance.Credits += validatedAmount;
                balance.LastPurchasedAt = DateTime.UtcNow;
                balance.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Maps a webhook variant_id to the correct credit amount using configured variants.
        /// If the variant_id is present and matches a known credit pack, the validated amount is used
        /// regardless of what the custom_data claims. If no variant_id is present, falls back to
        /// the claimed amount (backward compat) with a warning.
        /// </summary>
        private int ResolveValidatedCreditAmount(LsWebhookPayload payload, int claimedAmount)
        {
            if (TryMapCreditAmount(payload, out var variantAmount))
            {
                if (variantAmount != claimedAmount)
                {
                    _logger.LogWarning(
                        "Credit amount mismatch: variant maps to {ValidatedAmount} but custom_data claimed {ClaimedAmount}. Using validated amount.",
                        variantAmount, claimedAmount);
                }
                return variantAmount;
            }

            // No variant_id in payload or no matching credit variant configured — fall back to claimed amount
            _logger.LogWarning(
                "order_created has no recognizable credit variant_id; falling back to custom_data amount {Amount}",
                claimedAmount);
            return claimedAmount;
        }

        private bool TryMapCreditAmount(LsWebhookPayload payload, out int creditAmount)
        {
            creditAmount = 0;

            var variantElement = payload.Data.Attributes.VariantId;
            if (variantElement is not JsonElement element)
                return false;

            int variantId;
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (!element.TryGetInt32(out variantId))
                    return false;
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                if (!int.TryParse(element.GetString(), out variantId))
                    return false;
            }
            else
            {
                return false;
            }

            if (VariantMatches(_options.Variants.TreatBag10, variantId))
            {
                creditAmount = 10;
                return true;
            }
            if (VariantMatches(_options.Variants.TreatBag50, variantId))
            {
                creditAmount = 50;
                return true;
            }
            if (VariantMatches(_options.Variants.TreatBag100, variantId))
            {
                creditAmount = 100;
                return true;
            }

            return false;
        }

        private static bool VariantMatches(string configuredVariantId, int providerVariantId) =>
            int.TryParse(configuredVariantId, out var parsed) && parsed == providerVariantId;

        private static SubscriptionStatus MapLsStatus(string? status) => status?.ToLowerInvariant() switch
        {
            "active" => SubscriptionStatus.Active,
            "past_due" => SubscriptionStatus.PastDue,
            "cancelled" => SubscriptionStatus.Canceled,
            "expired" => SubscriptionStatus.Canceled,
            "on_trial" => SubscriptionStatus.Trialing,
            "paused" => SubscriptionStatus.PastDue,
            _ => SubscriptionStatus.PastDue
        };

        private static string? GetCustomDataValue(LsWebhookPayload payload, string key)
        {
            if (!TryGetCustomDataProperty(payload, key, out var value))
            {
                return null;
            }

            return GetElementValue(value);
        }

        private static int? GetCustomDataInt32(LsWebhookPayload payload, string key)
        {
            if (!TryGetCustomDataProperty(payload, key, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var numberValue))
            {
                return numberValue;
            }

            var stringValue = GetElementValue(value);
            return int.TryParse(stringValue, out var parsedValue) ? parsedValue : null;
        }

        private static bool TryGetCustomDataProperty(LsWebhookPayload payload, string key, out JsonElement value)
        {
            value = default;

            if (payload.Meta.CustomData is not JsonElement customData || customData.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in customData.EnumerateObject())
            {
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }

        private static string? GetAttributeValue(JsonElement? value) =>
            value is JsonElement element ? GetElementValue(element) : null;

        private static string? GetElementValue(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
            JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
            _ => value.GetRawText()
        };

        private static string? ResolveSubscriptionProviderId(LsWebhookPayload payload)
        {
            if (string.Equals(payload.Data.Type, "subscriptions", StringComparison.OrdinalIgnoreCase))
            {
                return payload.Data.Id;
            }

            return GetAttributeValue(payload.Data.Attributes.SubscriptionId)
                ?? GetAttributeValue(payload.Data.Attributes.FirstSubscriptionItem?.SubscriptionId);
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];
    }
}
