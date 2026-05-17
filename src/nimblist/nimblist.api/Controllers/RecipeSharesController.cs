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
    public class RecipeSharesController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly IPushNotificationService _pushNotificationService;

        public RecipeSharesController(NimblistContext context, IPushNotificationService pushNotificationService)
        {
            _context = context;
            _pushNotificationService = pushNotificationService;
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

            if (share.UserId != null)
            {
                var targetId = share.UserId;
                _ = Task.Run(() => _pushNotificationService.NotifyRecipeSharedAsync(targetId, recipe, currentUserId));
            }
            else if (share.FamilyId != null)
            {
                var memberIds = await _context.FamilyMembers
                    .Where(m => m.FamilyId == share.FamilyId && m.UserId != currentUserId)
                    .Select(m => m.UserId)
                    .ToListAsync();
                foreach (var memberId in memberIds)
                {
                    var id = memberId;
                    _ = Task.Run(() => _pushNotificationService.NotifyRecipeSharedAsync(id, recipe, currentUserId));
                }
            }

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

        [HttpPost("share-all")]
        public async Task<ActionResult<RecipeBulkShareResultDto>> PostBulkRecipeShare(RecipeBulkShareInputDto dto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            if (string.IsNullOrEmpty(dto.UserIdToShareWith) && !dto.FamilyIdToShareWith.HasValue)
                return BadRequest("Either UserIdToShareWith or FamilyIdToShareWith must be provided.");
            if (!string.IsNullOrEmpty(dto.UserIdToShareWith) && dto.FamilyIdToShareWith.HasValue)
                return BadRequest("Share with a user OR a family, not both.");

            if (!string.IsNullOrEmpty(dto.UserIdToShareWith))
            {
                if (dto.UserIdToShareWith == currentUserId) return BadRequest("Cannot share with yourself.");
                if (await _context.Users.FindAsync(dto.UserIdToShareWith) == null) return BadRequest("User not found.");
            }
            else
            {
                if (await _context.Families.FindAsync(dto.FamilyIdToShareWith!.Value) == null) return BadRequest("Family not found.");
            }

            var ownedRecipeIds = await _context.Recipes
                .Where(r => r.UserId == currentUserId)
                .Select(r => r.Id)
                .ToListAsync();

            var alreadySharedIds = !string.IsNullOrEmpty(dto.UserIdToShareWith)
                ? await _context.RecipeShares
                    .Where(rs => ownedRecipeIds.Contains(rs.RecipeId) && rs.UserId == dto.UserIdToShareWith)
                    .Select(rs => rs.RecipeId)
                    .ToHashSetAsync()
                : await _context.RecipeShares
                    .Where(rs => ownedRecipeIds.Contains(rs.RecipeId) && rs.FamilyId == dto.FamilyIdToShareWith)
                    .Select(rs => rs.RecipeId)
                    .ToHashSetAsync();

            var newShares = ownedRecipeIds
                .Where(id => !alreadySharedIds.Contains(id))
                .Select(id =>
                {
                    var share = new RecipeShare { Id = Guid.NewGuid(), RecipeId = id, SharedAt = DateTimeOffset.UtcNow };
                    if (!string.IsNullOrEmpty(dto.UserIdToShareWith)) share.UserId = dto.UserIdToShareWith;
                    else share.FamilyId = dto.FamilyIdToShareWith;
                    return share;
                })
                .ToList();

            _context.RecipeShares.AddRange(newShares);
            await _context.SaveChangesAsync();

            return Ok(new RecipeBulkShareResultDto(newShares.Count, alreadySharedIds.Count));
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
