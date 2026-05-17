using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nimblist.api.DTO;
using Nimblist.api.Services;
using Nimblist.Data.Models;
using System.Security.Claims;
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

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ISubscriptionService subscriptionService,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _subscriptionService = subscriptionService;
            _logger = logger;
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
        [Authorize] // IMPORTANT: Ensure only authenticated users can trigger logout
        // Note: [ValidateAntiForgeryToken] is recommended for POST actions with cookie auth
        // to prevent CSRF, but requires extra setup for SPA fetch calls.
        // Ensure your frontend isn't vulnerable if you omit it here.
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier); // Get ID for logging
            await _signInManager.SignOutAsync(); // Clears the .AspNetCore.Identity.Application cookie
            _logger.LogInformation("User {UserId} logged out.", userId);
            return Ok(new { message = "Logout successful" });
        }
    }

}