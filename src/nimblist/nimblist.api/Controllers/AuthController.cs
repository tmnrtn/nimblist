using Microsoft.AspNetCore.Authorization; // Needed for [Authorize], [AllowAnonymous]
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nimblist.api.DTO;
using Nimblist.Data.Models;
using System.Security.Claims; // Needed for ClaimTypes
using System.Threading.Tasks;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")] // Base route: api/auth
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<AuthController> _logger; // Optional: for logging

        // Inject UserManager and SignInManager via constructor
        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<AuthController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
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

            // Create a DTO (Data Transfer Object) to return ONLY necessary, non-sensitive info
            var userInfo = new UserInfoDto
            {
                UserId = user.Id,
                Email = user.Email,
                // You can add other claims or properties here if needed and safe
                // Name = User.FindFirstValue(ClaimTypes.Name), // Example getting Name claim
                // IsEmailConfirmed = user.EmailConfirmed // Example
            };

            return Ok(userInfo);
        }

        /// <summary>
        /// Logs the current user out by clearing the authentication cookie.
        /// Requires authentication.
        /// </summary>
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