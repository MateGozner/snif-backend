using System.Net.Http.Json;
using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SNIF.Core.Configuration;
using SNIF.Core.DTOs;
using System.Text;

namespace SNIF.Busniess.Services
{
    public class LemonSqueezyClient
    {
        private readonly HttpClient _httpClient;
        private readonly LemonSqueezyOptions _options;

        public LemonSqueezyClient(HttpClient httpClient, IOptions<LemonSqueezyOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;

            if (!string.IsNullOrWhiteSpace(_options.BaseUrl)
                && Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                _httpClient.BaseAddress = baseUri;
            }

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
            }

            if (!_httpClient.DefaultRequestHeaders.Accept.Any(header =>
                    string.Equals(header.MediaType, "application/vnd.api+json", StringComparison.OrdinalIgnoreCase)))
            {
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            }
        }

        public async Task<string> CreateCheckout(string variantId, Dictionary<string, string> customData, string? successUrl = null, string? email = null, string? name = null)
        {
            EnsureConfigured();

            // LemonSqueezy shows all product variants by default — restrict to only
            // the requested variant so the customer cannot switch to a cheaper one.
            if (!int.TryParse(variantId, out var variantIdInt))
                throw new InvalidOperationException($"Variant ID '{variantId}' is not a valid integer.");

            var productOptions = new LsCheckoutProductOptions
            {
                EnabledVariants = [variantIdInt]
            };

            if (!string.IsNullOrEmpty(successUrl))
            {
                productOptions.RedirectUrl = successUrl;
                productOptions.ReceiptButtonText = "Return to SNIF";
            }

            var request = new LsCheckoutRequest
            {
                Data = new LsCheckoutData
                {
                    Type = "checkouts",
                    Attributes = new LsCheckoutAttributes
                    {
                        CheckoutData = new LsCheckoutCustomData
                        {
                            Custom = customData,
                            Email = email,
                            Name = name
                        },
                        ProductOptions = productOptions
                    },
                    Relationships = new LsCheckoutRelationships
                    {
                        Store = new LsRelationship
                        {
                            Data = new LsRelationshipData { Type = "stores", Id = _options.StoreId }
                        },
                        Variant = new LsRelationship
                        {
                            Data = new LsRelationshipData { Type = "variants", Id = variantId }
                        }
                    }
                }
            };

            var response = await _httpClient.PostAsJsonAsync("/v1/checkouts", request);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var statusCode = (int)response.StatusCode;
                var message = new StringBuilder($"LemonSqueezy checkout failed with status {statusCode} ({response.StatusCode}).");

                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    message.Append(" Response body: ");
                    message.Append(responseBody);
                }

                throw new InvalidOperationException(message.ToString());
            }

            var result = await response.Content.ReadFromJsonAsync<LsCheckoutResponse>();
            return result?.Data?.Attributes?.Url
                ?? throw new InvalidOperationException("LemonSqueezy checkout did not return a URL.");
        }

        public async Task<LsSubscriptionData?> TryGetSubscription(string subscriptionId)
        {
            EnsureConfigured();

            var response = await _httpClient.GetAsync($"/v1/subscriptions/{subscriptionId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LsSubscriptionResponse>();
            return result?.Data
                ?? throw new InvalidOperationException("Failed to retrieve subscription.");
        }

        public async Task<IReadOnlyList<LsSubscriptionData>> ListSubscriptionsByEmail(string email)
        {
            EnsureConfigured();

            var path = QueryHelpers.AddQueryString("/v1/subscriptions", new Dictionary<string, string?>
            {
                ["filter[store_id]"] = _options.StoreId,
                ["filter[user_email]"] = email,
                ["page[size]"] = "100"
            });

            var response = await _httpClient.GetAsync(path);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LsSubscriptionListResponse>();
            return result?.Data ?? [];
        }

        public async Task<IReadOnlyList<LsOrderData>> ListOrdersByEmail(string email)
        {
            EnsureConfigured();

            var path = QueryHelpers.AddQueryString("/v1/orders", new Dictionary<string, string?>
            {
                ["filter[store_id]"] = _options.StoreId,
                ["filter[user_email]"] = email,
                ["page[size]"] = "100"
            });

            var response = await _httpClient.GetAsync(path);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LsOrderListResponse>();
            return result?.Data ?? [];
        }

        public async Task<string> CancelSubscription(string subscriptionId)
        {
            EnsureConfigured();

            var payload = new
            {
                data = new
                {
                    type = "subscriptions",
                    id = subscriptionId,
                    attributes = new { cancelled = true }
                }
            };

            var response = await _httpClient.PatchAsJsonAsync($"/v1/subscriptions/{subscriptionId}", payload);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<LsSubscriptionResponse>();
            return result?.Data?.Attributes?.Status ?? "cancelled";
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new InvalidOperationException("LemonSqueezy API key is not configured.");

            if (string.IsNullOrWhiteSpace(_options.StoreId))
                throw new InvalidOperationException("LemonSqueezy Store ID is not configured.");

            if (_httpClient.BaseAddress == null)
                throw new InvalidOperationException("LemonSqueezy Base URL is not configured.");
        }
    }
}
