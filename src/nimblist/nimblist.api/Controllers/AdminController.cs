using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NimblistContext _context;
        private readonly IPayPalService _payPal;

        public AdminController(UserManager<ApplicationUser> userManager, NimblistContext context, IPayPalService payPal)
        {
            _userManager = userManager;
            _context = context;
            _payPal = payPal;
        }

        // GET /api/admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _userManager.Users.ToListAsync();
            var result = new List<AdminUserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                result.Add(new AdminUserDto
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Roles = roles,
                });
            }

            return Ok(result);
        }

        // PUT /api/admin/users/{id}/role
        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> SetUserRole(string id, [FromBody] SetRoleDto dto)
        {
            if (dto.Role != "Admin" && dto.Role != "Standard")
                return BadRequest("Role must be 'Admin' or 'Standard'.");

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id == currentUserId)
                return BadRequest("You cannot change your own role.");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, dto.Role);

            return NoContent();
        }

        // DELETE /api/admin/users/{id}
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (id == currentUserId)
                return BadRequest("You cannot delete your own account.");

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
                return StatusCode(500, result.Errors);

            return NoContent();
        }

        // GET /api/admin/families
        [HttpGet("families")]
        public async Task<IActionResult> GetFamilies()
        {
            var families = await _context.Families
                .Include(f => f.Members)
                    .ThenInclude(m => m.User)
                .ToListAsync();

            var result = new List<AdminFamilyDto>();
            foreach (var family in families)
            {
                var owner = await _userManager.FindByIdAsync(family.UserId);
                result.Add(new AdminFamilyDto
                {
                    Id = family.Id,
                    Name = family.Name,
                    OwnerUserId = family.UserId,
                    OwnerEmail = owner?.Email,
                    Members = family.Members.Select(m => new AdminFamilyMemberDto
                    {
                        MemberId = m.Id,
                        UserId = m.UserId,
                        Email = m.User?.Email,
                        Role = m.Role,
                        JoinedAt = m.JoinedAt,
                    }).ToList(),
                });
            }

            return Ok(result);
        }

        // DELETE /api/admin/families/{familyId}/members/{memberId}
        [HttpDelete("families/{familyId}/members/{memberId}")]
        public async Task<IActionResult> RemoveFamilyMember(Guid familyId, Guid memberId)
        {
            var member = await _context.FamilyMembers
                .FirstOrDefaultAsync(m => m.FamilyId == familyId && m.Id == memberId);

            if (member == null) return NotFound();

            _context.FamilyMembers.Remove(member);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // DELETE /api/admin/families/{familyId}
        [HttpDelete("families/{familyId}")]
        public async Task<IActionResult> DeleteFamily(Guid familyId)
        {
            var family = await _context.Families.FindAsync(familyId);
            if (family == null) return NotFound();

            _context.Families.Remove(family);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // GET /api/admin/classification-feedback
        [HttpGet("classification-feedback")]
        public async Task<IActionResult> GetClassificationFeedback()
        {
            var rows = await _context.ClassificationFeedback
                .Include(f => f.Category)
                .Include(f => f.SubCategory)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            var userIds = rows.Select(r => r.UserId).Distinct().ToList();
            var userEmails = await _userManager.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Email })
                .ToDictionaryAsync(u => u.Id, u => u.Email);

            var result = rows.Select(f => new AdminFeedbackDto
            {
                Id = f.Id,
                ItemName = f.ItemName,
                CategoryName = f.Category?.Name,
                SubCategoryName = f.SubCategory?.Name,
                UserEmail = userEmails.TryGetValue(f.UserId, out var email) ? email : null,
                CreatedAt = f.CreatedAt,
            });

            return Ok(result);
        }

        // DELETE /api/admin/classification-feedback/{id}
        [HttpDelete("classification-feedback/{id:guid}")]
        public async Task<IActionResult> DeleteClassificationFeedback(Guid id)
        {
            var record = await _context.ClassificationFeedback.FindAsync(id);
            if (record == null) return NotFound();

            _context.ClassificationFeedback.Remove(record);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static readonly string[] ValidProviders = ["openrouter", "ollama", "openai", "anthropic", "gemini"];
        private static readonly Regex MaskedKeyPattern = new(@"\*{4}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));

        // GET /api/admin/llm-settings
        [HttpGet("llm-settings")]
        public async Task<IActionResult> GetLlmSettings()
        {
            var settings = await _context.LlmSettings.FirstOrDefaultAsync();
            if (settings == null)
                return Ok(new LlmSettingsDto());

            return Ok(new LlmSettingsDto
            {
                Provider = settings.Provider,
                Model = settings.Model,
                VisionModel = settings.VisionModel,
                ApiKey = MaskApiKey(settings.ApiKey),
                BaseUrl = settings.BaseUrl,
                ImageSearchApiKey = MaskApiKey(settings.ImageSearchApiKey),
                UpdatedAt = settings.UpdatedAt,
            });
        }

        // PUT /api/admin/llm-settings
        [HttpPut("llm-settings")]
        public async Task<IActionResult> UpdateLlmSettings([FromBody] LlmSettingsDto dto)
        {
            if (dto.Provider != null && !ValidProviders.Contains(dto.Provider.ToLower()))
                return BadRequest($"Provider must be one of: {string.Join(", ", ValidProviders)}");

            var settings = await _context.LlmSettings.FirstOrDefaultAsync()
                ?? new LlmSettings { Id = 1 };

            settings.Provider = dto.Provider?.ToLower().Trim();
            settings.Model = dto.Model?.Trim();
            settings.VisionModel = dto.VisionModel?.Trim();
            settings.BaseUrl = dto.BaseUrl?.Trim();
            settings.UpdatedAt = DateTimeOffset.UtcNow;

            // Only overwrite keys if a real value was sent (not the masked placeholder)
            if (dto.ApiKey != null && !MaskedKeyPattern.IsMatch(dto.ApiKey))
                settings.ApiKey = dto.ApiKey.Trim();
            if (dto.ImageSearchApiKey != null && !MaskedKeyPattern.IsMatch(dto.ImageSearchApiKey))
                settings.ImageSearchApiKey = dto.ImageSearchApiKey.Trim();

            if (settings.Id == 1 && !await _context.LlmSettings.AnyAsync())
                _context.LlmSettings.Add(settings);

            await _context.SaveChangesAsync();

            return Ok(new LlmSettingsDto
            {
                Provider = settings.Provider,
                Model = settings.Model,
                VisionModel = settings.VisionModel,
                ApiKey = MaskApiKey(settings.ApiKey),
                BaseUrl = settings.BaseUrl,
                ImageSearchApiKey = MaskApiKey(settings.ImageSearchApiKey),
                UpdatedAt = settings.UpdatedAt,
            });
        }

        // POST /api/admin/paypal/setup
        // One-time operation: creates a PayPal Product + Plan and returns the Plan ID.
        // Copy the returned planId into appsettings.json under PayPal:PlanId.
        [HttpPost("paypal/setup")]
        public async Task<IActionResult> SetupPayPalPlan()
        {
            try
            {
                var planId = await _payPal.CreateProductAndPlanAsync();
                return Ok(new { planId, message = "Plan created. Add this planId to appsettings.json under PayPal:PlanId." });
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { error = $"Failed to create PayPal plan: {ex.Message}" });
            }
        }

        private static string? MaskApiKey(string? key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            return key.Length <= 8 ? "****" : key[..4] + "****" + key[^4..];
        }
    }
}
