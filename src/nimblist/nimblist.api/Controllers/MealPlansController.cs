using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public class MealPlansController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly ISubscriptionService _subscriptionService;

        public MealPlansController(NimblistContext context, ISubscriptionService subscriptionService)
        {
            _context = context;
            _subscriptionService = subscriptionService;
        }

        private async Task<bool> RequirePaidAsync(string userId)
            => await _subscriptionService.HasActiveSubscriptionAsync(userId);

        private async Task<HashSet<Guid>> GetAccessibleMealPlanIdsAsync(string userId)
        {
            var ownIds = await _context.MealPlans.Where(m => m.UserId == userId).Select(m => m.Id).ToListAsync();
            var userSharedIds = await _context.MealPlanShares.Where(s => s.UserId == userId).Select(s => s.MealPlanId).ToListAsync();
            var familyIds = await _context.FamilyMembers.Where(m => m.UserId == userId).Select(m => m.FamilyId).ToListAsync();
            var familySharedIds = familyIds.Count > 0
                ? await _context.MealPlanShares.Where(s => s.FamilyId.HasValue && familyIds.Contains(s.FamilyId.Value)).Select(s => s.MealPlanId).ToListAsync()
                : new List<Guid>();
            return ownIds.Concat(userSharedIds).Concat(familySharedIds).ToHashSet();
        }

        // GET /api/mealplans
        [HttpGet]
        public async Task<ActionResult<List<MealPlanSummaryDto>>> GetMealPlans()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await RequirePaidAsync(userId)) return StatusCode(403, new { reason = "subscription_required" });

            var accessibleIds = await GetAccessibleMealPlanIdsAsync(userId);

            var plans = await _context.MealPlans
                .Where(m => accessibleIds.Contains(m.Id))
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MealPlanSummaryDto(m.Id, m.Name, m.UserId, m.UserId == userId, m.CreatedAt))
                .ToListAsync();

            return Ok(plans);
        }

        // POST /api/mealplans
        [HttpPost]
        public async Task<ActionResult<MealPlanSummaryDto>> CreateMealPlan([FromBody] CreateMealPlanRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await RequirePaidAsync(userId)) return StatusCode(403, new { reason = "subscription_required" });

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Name is required.");

            var plan = new MealPlan
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            _context.MealPlans.Add(plan);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetMealPlans), new { id = plan.Id },
                new MealPlanSummaryDto(plan.Id, plan.Name, plan.UserId, true, plan.CreatedAt));
        }

        // GET /api/mealplans/{id}/entries?from=2026-05-05&to=2026-05-11
        [HttpGet("{id}/entries")]
        public async Task<ActionResult<List<MealPlanEntryDto>>> GetEntries(Guid id, [FromQuery] DateOnly from, [FromQuery] DateOnly to)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await RequirePaidAsync(userId)) return StatusCode(403, new { reason = "subscription_required" });

            var accessibleIds = await GetAccessibleMealPlanIdsAsync(userId);
            if (!accessibleIds.Contains(id)) return NotFound();

            var entries = await _context.MealPlanEntries
                .Include(e => e.Recipe)
                .Where(e => e.MealPlanId == id && e.PlannedDate >= from && e.PlannedDate <= to)
                .OrderBy(e => e.PlannedDate)
                .ToListAsync();

            return Ok(entries.Select(e => new MealPlanEntryDto(
                e.Id, e.MealPlanId, e.RecipeId,
                e.Recipe?.Title ?? "Unknown Recipe",
                e.Recipe?.ImageUrl,
                e.PlannedDate, e.MealType, e.Notes)));
        }

        // DELETE /api/mealplans/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMealPlan(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await RequirePaidAsync(userId)) return StatusCode(403, new { reason = "subscription_required" });

            var plan = await _context.MealPlans.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (plan == null) return NotFound();

            _context.MealPlans.Remove(plan);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
