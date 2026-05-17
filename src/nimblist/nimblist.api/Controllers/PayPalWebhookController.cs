using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.Services;
using Nimblist.Data;
using System.Text.Json;

namespace Nimblist.api.Controllers
{
    [Route("api/paypal")]
    [ApiController]
    public class PayPalWebhookController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly IPayPalService _payPal;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PayPalWebhookController> _logger;

        public PayPalWebhookController(
            NimblistContext context,
            IPayPalService payPal,
            IConfiguration configuration,
            ILogger<PayPalWebhookController> logger)
        {
            _context = context;
            _payPal = payPal;
            _configuration = configuration;
            _logger = logger;
        }

        // POST /api/paypal/webhook
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleWebhook()
        {
            string rawBody;
            using (var reader = new StreamReader(Request.Body))
                rawBody = await reader.ReadToEndAsync();

            // Verify signature — skip in dev if WebhookId not configured
            var webhookId = _configuration["PayPal:WebhookId"];
            if (!string.IsNullOrEmpty(webhookId))
            {
                var valid = await _payPal.VerifyWebhookSignatureAsync(
                    Request.Headers["PAYPAL-TRANSMISSION-ID"].FirstOrDefault() ?? "",
                    Request.Headers["PAYPAL-TRANSMISSION-TIME"].FirstOrDefault() ?? "",
                    Request.Headers["PAYPAL-CERT-URL"].FirstOrDefault() ?? "",
                    Request.Headers["PAYPAL-AUTH-ALGO"].FirstOrDefault() ?? "",
                    Request.Headers["PAYPAL-TRANSMISSION-SIG"].FirstOrDefault() ?? "",
                    webhookId,
                    rawBody);

                if (!valid)
                {
                    _logger.LogWarning("PayPal webhook signature verification failed");
                    return Unauthorized();
                }
            }

            JsonElement payload;
            try { payload = JsonSerializer.Deserialize<JsonElement>(rawBody); }
            catch { return BadRequest(); }

            if (!payload.TryGetProperty("event_type", out var eventTypeEl)) return Ok();
            var eventType = eventTypeEl.GetString() ?? "";

            if (!payload.TryGetProperty("resource", out var resource)) return Ok();

            var subscriptionId = resource.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (string.IsNullOrEmpty(subscriptionId))
            {
                _logger.LogWarning("PayPal webhook {EventType} missing resource.id", eventType);
                return Ok();
            }

            _logger.LogInformation("PayPal webhook {EventType} for subscription {SubId}", eventType, subscriptionId);

            var sub = await _context.UserSubscriptions
                .FirstOrDefaultAsync(s => s.PayPalSubscriptionId == subscriptionId);

            if (sub == null)
            {
                _logger.LogWarning("No local subscription found for PayPal id {SubId}", subscriptionId);
                return Ok();
            }

            switch (eventType)
            {
                case "BILLING.SUBSCRIPTION.ACTIVATED":
                    sub.Status = "ACTIVE";
                    // Once activated after trial payment, trial is over
                    if (sub.IsInTrial && sub.TrialEndDate.HasValue && DateTime.UtcNow > sub.TrialEndDate.Value)
                        sub.IsInTrial = false;
                    break;

                case "BILLING.SUBSCRIPTION.CANCELLED":
                    sub.Status = "CANCELLED";
                    break;

                case "BILLING.SUBSCRIPTION.SUSPENDED":
                    sub.Status = "SUSPENDED";
                    break;

                case "BILLING.SUBSCRIPTION.EXPIRED":
                    sub.Status = "EXPIRED";
                    break;

                case "PAYMENT.SALE.COMPLETED":
                    sub.IsInTrial = false;
                    if (resource.TryGetProperty("billing_agreement_id", out var baId) && baId.GetString() == subscriptionId)
                    {
                        // Update next billing date — PayPal doesn't send it in this event so re-fetch
                        var details = await _payPal.GetSubscriptionAsync(subscriptionId);
                        if (details?.BillingInfo?.NextBillingTime.HasValue == true)
                            sub.NextBillingDate = details.BillingInfo.NextBillingTime;
                    }
                    break;
            }

            sub.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}
