using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Security.Claims;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SubscriptionController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly IPayPalService _payPal;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SubscriptionController> _logger;
        private readonly ISubscriptionEmailService _subscriptionEmail;
        private readonly UserManager<ApplicationUser> _userManager;

        public SubscriptionController(
            NimblistContext context,
            IPayPalService payPal,
            IConfiguration configuration,
            ILogger<SubscriptionController> logger,
            ISubscriptionEmailService subscriptionEmail,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _payPal = payPal;
            _configuration = configuration;
            _logger = logger;
            _subscriptionEmail = subscriptionEmail;
            _userManager = userManager;
        }

        // GET /api/subscription
        [HttpGet]
        public async Task<IActionResult> GetStatus()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var sub = await _context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
            if (sub == null)
                return Ok(new SubscriptionStatusDto { Tier = "free" });

            var isActive = sub.Status == "ACTIVE" || sub.Status == "APPROVED";
            return Ok(new SubscriptionStatusDto
            {
                Tier = isActive ? "paid" : "free",
                Status = sub.Status,
                IsInTrial = sub.IsInTrial,
                TrialEndDate = sub.TrialEndDate,
                NextBillingDate = sub.NextBillingDate,
                PayPalSubscriptionId = sub.PayPalSubscriptionId,
            });
        }

        // GET /api/subscription/config — returns public PayPal config needed by the frontend
        [HttpGet("config")]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                clientId = _configuration["PayPal:ClientId"],
                planId = _configuration["PayPal:PlanId"],
            });
        }

        // POST /api/subscription/activate — called after PayPal approval
        [HttpPost("activate")]
        [EnableRateLimiting("subscription-activate")]
        public async Task<IActionResult> Activate([FromBody] ActivateSubscriptionRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.SubscriptionId))
                return BadRequest("SubscriptionId is required.");

            var details = await _payPal.GetSubscriptionAsync(request.SubscriptionId);
            if (details == null)
                return UnprocessableEntity(new { error = "Could not verify subscription with PayPal." });

            if (details.Status != "ACTIVE" && details.Status != "APPROVED")
                return UnprocessableEntity(new { error = $"Subscription is not active (status: {details.Status})." });

            // Determine trial status from cycle executions
            var inTrial = details.BillingInfo?.CycleExecutions
                ?.Any(c => c.TenureType == "TRIAL" && c.CyclesCompleted == 0) ?? true;

            var trialEndDate = inTrial && details.StartTime.HasValue
                ? details.StartTime.Value.AddDays(7)
                : (DateTime?)null;

            var nextBilling = details.BillingInfo?.NextBillingTime;

            var existing = await _context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
            if (existing != null)
            {
                existing.PayPalSubscriptionId = details.Id;
                existing.Status = details.Status;
                existing.IsInTrial = inTrial;
                existing.TrialEndDate = trialEndDate;
                existing.NextBillingDate = nextBilling;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.UserSubscriptions.Add(new UserSubscription
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PayPalSubscriptionId = details.Id,
                    Status = details.Status,
                    IsInTrial = inTrial,
                    TrialEndDate = trialEndDate,
                    NextBillingDate = nextBilling,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                });
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Subscription {SubId} activated for user {UserId}", details.Id, userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user?.Email is not null)
                _ = _subscriptionEmail.SendSubscriptionActivatedAsync(user.Email, inTrial, trialEndDate);

            return Ok(new SubscriptionStatusDto
            {
                Tier = "paid",
                Status = details.Status,
                IsInTrial = inTrial,
                TrialEndDate = trialEndDate,
                NextBillingDate = nextBilling,
                PayPalSubscriptionId = details.Id,
            });
        }

        // POST /api/subscription/cancel
        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var sub = await _context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
            if (sub == null) return NotFound("No active subscription found.");

            var cancelled = await _payPal.CancelSubscriptionAsync(sub.PayPalSubscriptionId, "Cancelled by user");
            if (!cancelled)
            {
                _logger.LogWarning("PayPal cancellation API call failed for subscription {SubId}", sub.PayPalSubscriptionId);
                return StatusCode(502, new { error = "Failed to cancel subscription with PayPal." });
            }

            sub.Status = "CANCELLED";
            sub.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscription {SubId} cancelled for user {UserId}", sub.PayPalSubscriptionId, userId);

            var user = await _userManager.FindByIdAsync(userId);
            if (user?.Email is not null)
                _ = _subscriptionEmail.SendSubscriptionCancelledAsync(user.Email);

            return Ok(new { message = "Subscription cancelled." });
        }
    }
}
