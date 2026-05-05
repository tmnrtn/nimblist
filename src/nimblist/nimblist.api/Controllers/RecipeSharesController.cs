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
    public class RecipeSharesController : ControllerBase
    {
        private readonly NimblistContext _context;

        public RecipeSharesController(NimblistContext context)
        {
            _context = context;
        }

        private static RecipeShareDetailDto ToDto(RecipeShare rs) =>
            new(rs.Id, rs.RecipeId, rs.UserId, rs.User?.Email, rs.FamilyId, rs.Family?.Name, rs.SharedAt);

        [HttpPost]
        public async Task<ActionResult<RecipeShareDetailDto>> PostRecipeShare(RecipeShareInputDto dto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            if (string.IsNullOrEmpty(dto.UserIdToShareWith) && !dto.FamilyIdToShareWith.HasValue)
                return BadRequest("Either UserIdToShareWith or FamilyIdToShareWith must be provided.");
            if (!string.IsNullOrEmpty(dto.UserIdToShareWith) && dto.FamilyIdToShareWith.HasValue)
                return BadRequest("Share with a user OR a family, not both.");

            var recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == dto.RecipeId);
            if (recipe == null) return NotFound("Recipe not found.");
            if (recipe.UserId != currentUserId) return Forbid();

            bool duplicate = !string.IsNullOrEmpty(dto.UserIdToShareWith)
                ? await _context.RecipeShares.AnyAsync(rs => rs.RecipeId == dto.RecipeId && rs.UserId == dto.UserIdToShareWith)
                : await _context.RecipeShares.AnyAsync(rs => rs.RecipeId == dto.RecipeId && rs.FamilyId == dto.FamilyIdToShareWith);

            if (duplicate) return Conflict("Already shared with that user or family.");

            var share = new RecipeShare { Id = Guid.NewGuid(), RecipeId = dto.RecipeId, SharedAt = DateTimeOffset.UtcNow };
            var targetError = await ApplyShareTargetAsync(share, dto.UserIdToShareWith, dto.FamilyIdToShareWith, currentUserId);
            if (targetError != null) return targetError;

            _context.RecipeShares.Add(share);
            await _context.SaveChangesAsync();

            if (share.UserId != null) await _context.Entry(share).Reference(s => s.User).LoadAsync();
            if (share.FamilyId != null) await _context.Entry(share).Reference(s => s.Family).LoadAsync();

            return CreatedAtAction(nameof(GetSharesForRecipe), new { recipeId = share.RecipeId }, ToDto(share));
        }

        private async Task<BadRequestObjectResult?> ApplyShareTargetAsync(
            RecipeShare share, string? userIdToShareWith, Guid? familyIdToShareWith, string currentUserId)
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecipeShare(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var share = await _context.RecipeShares.Include(s => s.Recipe).FirstOrDefaultAsync(s => s.Id == id);
            if (share == null) return NotFound();
            if (share.Recipe!.UserId != currentUserId) return Forbid();

            _context.RecipeShares.Remove(share);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<RecipeShareDetailDto>>> GetSharesForRecipe([FromQuery] Guid recipeId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var recipe = await _context.Recipes.FindAsync(recipeId);
            if (recipe == null) return NotFound();
            if (recipe.UserId != currentUserId) return Forbid();

            var shares = await _context.RecipeShares
                .Where(s => s.RecipeId == recipeId)
                .Include(s => s.User)
                .Include(s => s.Family)
                .ToListAsync();

            return Ok(shares.Select(ToDto));
        }
    }
}
