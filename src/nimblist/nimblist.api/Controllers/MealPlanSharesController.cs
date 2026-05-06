using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Security.Claims;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MealPlanSharesController : ControllerBase
    {
        private readonly NimblistContext _context;

        public MealPlanSharesController(NimblistContext context)
        {
            _context = context;
        }

        private static MealPlanShareDetailDto ToDto(MealPlanShare s) =>
            new(s.Id, s.MealPlanId, s.UserId, s.User?.Email, s.FamilyId, s.Family?.Name, s.SharedAt);

        // GET /api/mealplanshares?mealPlanId={id}
        [HttpGet]
        public async Task<ActionResult<IEnumerable<MealPlanShareDetailDto>>> GetSharesForPlan([FromQuery] Guid mealPlanId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var plan = await _context.MealPlans.FindAsync(mealPlanId);
            if (plan == null) return NotFound();
            if (plan.UserId != userId) return Forbid();

            var shares = await _context.MealPlanShares
                .Where(s => s.MealPlanId == mealPlanId)
                .Include(s => s.User)
                .Include(s => s.Family)
                .ToListAsync();

            return Ok(shares.Select(ToDto));
        }

        // POST /api/mealplanshares
        [HttpPost]
        public async Task<ActionResult<MealPlanShareDetailDto>> PostShare(MealPlanShareInputDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrEmpty(dto.UserIdToShareWith) && !dto.FamilyIdToShareWith.HasValue)
                return BadRequest("Either UserIdToShareWith or FamilyIdToShareWith must be provided.");
            if (!string.IsNullOrEmpty(dto.UserIdToShareWith) && dto.FamilyIdToShareWith.HasValue)
                return BadRequest("Share with a user OR a family, not both.");

            var plan = await _context.MealPlans.FirstOrDefaultAsync(m => m.Id == dto.MealPlanId);
            if (plan == null) return NotFound("Meal plan not found.");
            if (plan.UserId != userId) return Forbid();

            bool duplicate = !string.IsNullOrEmpty(dto.UserIdToShareWith)
                ? await _context.MealPlanShares.AnyAsync(s => s.MealPlanId == dto.MealPlanId && s.UserId == dto.UserIdToShareWith)
                : await _context.MealPlanShares.AnyAsync(s => s.MealPlanId == dto.MealPlanId && s.FamilyId == dto.FamilyIdToShareWith);

            if (duplicate) return Conflict("Already shared with that user or family.");

            var share = new MealPlanShare { Id = Guid.NewGuid(), MealPlanId = dto.MealPlanId, SharedAt = DateTimeOffset.UtcNow };
            var targetError = await ApplyShareTargetAsync(share, dto.UserIdToShareWith, dto.FamilyIdToShareWith, userId);
            if (targetError != null) return targetError;

            _context.MealPlanShares.Add(share);
            await _context.SaveChangesAsync();

            if (share.UserId != null) await _context.Entry(share).Reference(s => s.User).LoadAsync();
            if (share.FamilyId != null) await _context.Entry(share).Reference(s => s.Family).LoadAsync();

            return CreatedAtAction(nameof(GetSharesForPlan), new { mealPlanId = share.MealPlanId }, ToDto(share));
        }

        private async Task<BadRequestObjectResult?> ApplyShareTargetAsync(
            MealPlanShare share, string? userIdToShareWith, Guid? familyIdToShareWith, string currentUserId)
        {
            if (!string.IsNullOrEmpty(userIdToShareWith))
            {
                if (userIdToShareWith == currentUserId) return BadRequest("Cannot share with yourself.");
                if (await _context.Users.FindAsync(userIdToShareWith) == null) return BadRequest("User not found.");
                share.UserId = userIdToShareWith;
            }
            else
            {
                if (await _context.Families.FindAsync(familyIdToShareWith!.Value) == null) return BadRequest("Family not found.");
                share.FamilyId = familyIdToShareWith;
            }
            return null;
        }

        // DELETE /api/mealplanshares/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteShare(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var share = await _context.MealPlanShares.Include(s => s.MealPlan).FirstOrDefaultAsync(s => s.Id == id);
            if (share == null) return NotFound();
            if (share.MealPlan!.UserId != userId) return Forbid();

            _context.MealPlanShares.Remove(share);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
