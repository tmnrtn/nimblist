using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ISubscriptionService _subscriptionService;
        private readonly ILogger<AuthController> _logger;
        private readonly NimblistContext _context;
        private readonly IPayPalService _payPal;
        private readonly IConfiguration _configuration;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ISubscriptionService subscriptionService,
            ILogger<AuthController> logger,
            NimblistContext context,
            IPayPalService payPal,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _subscriptionService = subscriptionService;
            _logger = logger;
            _context = context;
            _payPal = payPal;
            _configuration = configuration;
        }

        /// <summary>
        /// Gets information about the currently authenticated user.
        /// Requires authentication (valid cookie).
        /// Returns user details if logged in, otherwise 401 Unauthorized.
        /// </summary>
        [HttpGet("userinfo")]
        [Authorize] // IMPORTANT: Ensures only authenticated users can access this
        [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetUserInfo()
        {
            // The [Authorize] attribute ensures User.Identity.IsAuthenticated is true.
            // We still need to fetch the user details from the database.
            var user = await _userManager.GetUserAsync(User); // User comes from ControllerBase

            if (user == null)
            {
                // This shouldn't happen if [Authorize] is working correctly with the cookie,
                // but handle defensively. Could indicate cookie valid but user deleted?
                _logger.LogWarning("User info requested for authenticated user but user not found (ID: {UserId}).",
                    User.FindFirstValue(ClaimTypes.NameIdentifier));
                return Unauthorized(new { message = "User not found despite valid authentication." });
            }

            var roles = await _userManager.GetRolesAsync(user);
            var subStatus = await _subscriptionService.GetSubscriptionStatusAsync(user.Id);

            var userInfo = new UserInfoDto
            {
                UserId = user.Id,
                Email = user.Email,
                Roles = roles,
                SubscriptionTier = subStatus?.Tier ?? "free",
                IsInTrial = subStatus?.IsInTrial ?? false,
                TrialEndDate = subStatus?.TrialEndDate,
            };

            return Ok(userInfo);
        }

        /// <summary>
        /// Logs the current user out by clearing the authentication cookie.
        /// Requires authentication.
        /// </summary>
        [HttpGet("lookup")]
        [Authorize]
        public async Task<IActionResult> LookupUser([FromQuery] string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return BadRequest("Email is required.");
            var user = await _userManager.FindByEmailAsync(email.Trim());
            if (user == null) return NotFound("No user found with that email address.");
            return Ok(new { userId = user.Id, email = user.Email });
        }

        [HttpPost("logout")]
        [Authorize]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User {UserId} logged out.", userId);
            return Ok(new { message = "Logout successful" });
        }

        // DELETE /api/auth/account — self-service account deletion (GDPR right to erasure)
        [HttpDelete("account")]
        [Authorize]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Cancel active PayPal subscription before deleting
            var sub = await _context.UserSubscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
            if (sub is { Status: "ACTIVE" or "APPROVED" })
            {
                try { await _payPal.CancelSubscriptionAsync(sub.PayPalSubscriptionId, "Account deleted by user"); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not cancel PayPal subscription {SubId} during account deletion", sub.PayPalSubscriptionId); }
            }

            await _signInManager.SignOutAsync();

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to delete user {UserId}: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return StatusCode(500, new { error = "Failed to delete account. Please contact support." });
            }

            _logger.LogInformation("User {UserId} deleted their account.", userId);
            return Ok(new { message = "Account deleted." });
        }

        // GET /api/auth/invite — returns the current user's invite link
        [HttpGet("invite")]
        [Authorize]
        public async Task<IActionResult> GetInvite()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            if (string.IsNullOrEmpty(user.InviteCode))
            {
                user.InviteCode = GenerateInviteCode();
                await _userManager.UpdateAsync(user);
            }

            var frontendBase = _configuration["FrontendAppSettings:BaseUrl"]?.TrimEnd('/') ?? "";
            var inviteUrl = $"{frontendBase}/?invite={user.InviteCode}";
            return Ok(new { inviteCode = user.InviteCode, inviteUrl });
        }

        // POST /api/auth/claim-invite — links the current user to a referrer
        [HttpPost("claim-invite")]
        [Authorize]
        public async Task<IActionResult> ClaimInvite([FromBody] ClaimInviteRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return Unauthorized();

            if (!string.IsNullOrEmpty(user.InvitedByUserId))
                return Ok(); // already claimed — no-op

            var referrer = await _context.Users.FirstOrDefaultAsync(u => u.InviteCode == request.Code);
            if (referrer == null) return NotFound(new { error = "Invalid invite code." });
            if (referrer.Id == userId) return BadRequest(new { error = "Cannot use your own invite code." });

            user.InvitedByUserId = referrer.Id;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("User {UserId} claimed invite from referrer {ReferrerId}.", userId, referrer.Id);
            return Ok();
        }

        private static string GenerateInviteCode()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = new byte[8];
            RandomNumberGenerator.Fill(bytes);
            return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
        }

        // GET /api/auth/export — GDPR right to data portability
        [HttpGet("export")]
        [Authorize]
        public async Task<IActionResult> ExportData()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var lists = await _context.ShoppingLists
                .Include(l => l.Items)
                .Where(l => l.UserId == userId)
                .Select(l => new
                {
                    l.Id, l.Name, l.IsTemplate,
                    Items = l.Items.Select(i => new { i.Id, i.Name, i.Quantity, i.IsChecked })
                })
                .ToListAsync();

            var recipes = await _context.Recipes
                .Include(r => r.Ingredients)
                .Where(r => r.UserId == userId)
                .Select(r => new
                {
                    r.Id, r.Title, r.Description, r.SourceUrl, r.ImageUrl,
                    Ingredients = r.Ingredients.Select(i => new { i.Text, i.ParsedName, i.ParsedQuantity })
                })
                .ToListAsync();

            var mealPlans = await _context.MealPlans
                .Include(m => m.Entries)
                .Where(m => m.UserId == userId)
                .Select(m => new
                {
                    m.Id, m.Name,
                    Entries = m.Entries.Select(e => new { e.PlannedDate, e.MealType, e.RecipeId })
                })
                .ToListAsync();

            var subscription = await _context.UserSubscriptions
                .Where(s => s.UserId == userId)
                .Select(s => new { s.Status, s.IsInTrial, s.TrialEndDate, s.NextBillingDate, s.CreatedAt })
                .FirstOrDefaultAsync();

            var export = new
            {
                ExportedAt = DateTime.UtcNow,
                Profile = new { user.Email, user.UserName, CreatedAt = user.LockoutEnd },
                ShoppingLists = lists,
                Recipes = recipes,
                MealPlans = mealPlans,
                Subscription = subscription,
            };

            return File(
                System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(export, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }),
                "application/json",
                $"nimblist-export-{DateTime.UtcNow:yyyyMMdd}.json");
        }
    }

}