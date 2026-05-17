using Nimblist.api.DTO;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nimblist.api.Services
{
    public class PayPalService : IPayPalService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayPalService> _logger;

        private string BaseUrl => _configuration["PayPal:Mode"] == "live"
            ? "https://api-m.paypal.com"
            : "https://api-m.sandbox.paypal.com";

        public PayPalService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<PayPalService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        private async Task<string> GetAccessTokenAsync()
        {
            var clientId = _configuration["PayPal:ClientId"];
            var secret = _configuration["PayPal:ClientSecret"];

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
                throw new InvalidOperationException("PayPal credentials not configured.");

            var client = _httpClientFactory.CreateClient("PayPal");
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{secret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "client_credentials")
            });

            var response = await client.PostAsync($"{BaseUrl}/v1/oauth2/token", content);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("No access_token in PayPal response.");
        }

        public async Task<PayPalSubscriptionDetails?> GetSubscriptionAsync(string subscriptionId)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                var client = _httpClientFactory.CreateClient("PayPal");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await client.GetAsync($"{BaseUrl}/v1/billing/subscriptions/{subscriptionId}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("PayPal subscription lookup failed for {Id}: {Status}", subscriptionId, response.StatusCode);
                    return null;
                }

                return await response.Content.ReadFromJsonAsync<PayPalSubscriptionDetails>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching PayPal subscription {Id}", subscriptionId);
                return null;
            }
        }

        public async Task<bool> CancelSubscriptionAsync(string subscriptionId, string reason)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                var client = _httpClientFactory.CreateClient("PayPal");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var content = JsonContent.Create(new { reason });
                var response = await client.PostAsync($"{BaseUrl}/v1/billing/subscriptions/{subscriptionId}/cancel", content);
                return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NoContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling PayPal subscription {Id}", subscriptionId);
                return false;
            }
        }

        public async Task<bool> VerifyWebhookSignatureAsync(
            string transmissionId, string transmissionTime, string certUrl,
            string authAlgo, string transmissionSig, string webhookId, string rawBody)
        {
            try
            {
                var token = await GetAccessTokenAsync();
                var client = _httpClientFactory.CreateClient("PayPal");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var payload = new
                {
                    transmission_id = transmissionId,
                    transmission_time = transmissionTime,
                    cert_url = certUrl,
                    auth_algo = authAlgo,
                    transmission_sig = transmissionSig,
                    webhook_id = webhookId,
                    webhook_event = JsonSerializer.Deserialize<JsonElement>(rawBody),
                };

                var response = await client.PostAsJsonAsync($"{BaseUrl}/v1/notifications/verify-webhook-signature", payload);
                if (!response.IsSuccessStatusCode) return false;

                var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                return result.TryGetProperty("verification_status", out var status) &&
                       status.GetString() == "SUCCESS";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying PayPal webhook signature");
                return false;
            }
        }

        public async Task<string> CreateProductAndPlanAsync()
        {
            var token = await GetAccessTokenAsync();
            var client = _httpClientFactory.CreateClient("PayPal");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Create product
            var productResponse = await client.PostAsJsonAsync($"{BaseUrl}/v1/catalogs/products", new
            {
                name = "Nimblist Premium",
                type = "SERVICE",
                category = "SOFTWARE",
            });
            productResponse.EnsureSuccessStatusCode();
            var product = await productResponse.Content.ReadFromJsonAsync<JsonElement>();
            var productId = product.GetProperty("id").GetString()!;

            // Create plan with 7-day trial then £1.99/month
            var planResponse = await client.PostAsJsonAsync($"{BaseUrl}/v1/billing/plans", new
            {
                product_id = productId,
                name = "Nimblist Premium Monthly",
                status = "ACTIVE",
                billing_cycles = new object[]
                {
                    new
                    {
                        frequency = new { interval_unit = "DAY", interval_count = 7 },
                        tenure_type = "TRIAL",
                        sequence = 1,
                        total_cycles = 1,
                        pricing_scheme = new { fixed_price = new { value = "0", currency_code = "GBP" } }
                    },
                    new
                    {
                        frequency = new { interval_unit = "MONTH", interval_count = 1 },
                        tenure_type = "REGULAR",
                        sequence = 2,
                        total_cycles = 0,
                        pricing_scheme = new { fixed_price = new { value = "1.99", currency_code = "GBP" } }
                    }
                },
                payment_preferences = new
                {
                    auto_bill_outstanding = true,
                    setup_fee_failure_action = "CANCEL",
                    payment_failure_threshold = 3
                }
            });
            planResponse.EnsureSuccessStatusCode();
            var plan = await planResponse.Content.ReadFromJsonAsync<JsonElement>();
            return plan.GetProperty("id").GetString()!;
        }
    }
}
