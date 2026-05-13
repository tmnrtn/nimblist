using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.api.Hubs;
using Nimblist.api.Services;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Security.Claims;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MealPlanEntriesController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly IClassificationService _classificationService;
        private readonly IHubContext<ShoppingListHub> _hubContext;

        public MealPlanEntriesController(
            NimblistContext context,
            IClassificationService classificationService,
            IHubContext<ShoppingListHub> hubContext)
        {
            _context = context;
            _classificationService = classificationService;
            _hubContext = hubContext;
        }

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

        // POST /api/mealplanentries
        [HttpPost]
        public async Task<ActionResult<MealPlanEntryDto>> CreateEntry([FromBody] CreateMealPlanEntryRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var accessibleIds = await GetAccessibleMealPlanIdsAsync(userId);
            if (!accessibleIds.Contains(request.MealPlanId)) return NotFound("Meal plan not found.");

            var recipe = await _context.Recipes.FindAsync(request.RecipeId);
            if (recipe == null) return NotFound("Recipe not found.");

            var entry = new MealPlanEntry
            {
                Id = Guid.NewGuid(),
                MealPlanId = request.MealPlanId,
                RecipeId = request.RecipeId,
                PlannedDate = request.PlannedDate,
                MealType = request.MealType,
                Notes = request.Notes,
            };

            _context.MealPlanEntries.Add(entry);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(DeleteEntry), new { id = entry.Id },
                new MealPlanEntryDto(entry.Id, entry.MealPlanId, entry.RecipeId,
                    recipe.Title, recipe.ImageUrl, entry.PlannedDate, entry.MealType, entry.Notes));
        }

        // DELETE /api/mealplanentries/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEntry(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var entry = await _context.MealPlanEntries
                .Include(e => e.MealPlan)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null) return NotFound();

            // Only the plan owner can delete entries
            if (entry.MealPlan!.UserId != userId) return Forbid();

            _context.MealPlanEntries.Remove(entry);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST /api/mealplanentries/{id}/addtolist/{listId}
        [HttpPost("{id}/addtolist/{listId}")]
        public async Task<ActionResult> AddEntryToList(Guid id, Guid listId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var entry = await _context.MealPlanEntries
                .Include(e => e.MealPlan)
                .Include(e => e.Recipe)
                    .ThenInclude(r => r!.Ingredients)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (entry == null) return NotFound("Entry not found.");

            var accessibleIds = await GetAccessibleMealPlanIdsAsync(userId);
            if (!accessibleIds.Contains(entry.MealPlanId)) return Forbid();

            var listExists = await _context.ShoppingLists.AnyAsync(sl => sl.Id == listId && sl.UserId == userId);
            if (!listExists) return NotFound("Shopping list not found.");

            // Load existing items once for deduplication; updated in-loop for intra-recipe dupes
            var existingItems = await _context.Items
                .Where(i => i.ShoppingListId == listId)
                .ToListAsync();

            var addedItems = new List<ItemWithCategoryDto>();

            foreach (var ingredient in entry.Recipe!.Ingredients.OrderBy(i => i.SortOrder))
            {
                var itemName = ingredient.ParsedName ?? ingredient.Text;
                var quantity = ingredient.ParsedQuantity;

                var existing = existingItems.FirstOrDefault(i =>
                    string.Equals(i.Name, itemName, StringComparison.OrdinalIgnoreCase));

                ItemWithCategoryDto dto;

                if (existing != null)
                {
                    existing.Quantity = Services.QuantityHelper.Merge(existing.Quantity, quantity);
                    await _context.SaveChangesAsync();

                    await _context.Entry(existing).Reference(i => i.Category).LoadAsync();
                    await _context.Entry(existing).Reference(i => i.SubCategory).LoadAsync();

                    dto = new ItemWithCategoryDto
                    {
                        Id = existing.Id,
                        Name = existing.Name,
                        Quantity = existing.Quantity,
                        IsChecked = existing.IsChecked,
                        AddedAt = existing.AddedAt,
                        ShoppingListId = existing.ShoppingListId,
                        CategoryId = existing.CategoryId,
                        CategoryName = existing.Category?.Name,
                        SubCategoryId = existing.SubCategoryId,
                        SubCategoryName = existing.SubCategory?.Name,
                    };

                    await _hubContext.Clients
                        .Group($"list_{listId}")
                        .SendAsync("ReceiveItemUpdated", dto);
                }
                else
                {
                    var (categoryId, subCategoryId) = await _classificationService.ClassifyAsync(itemName);

                    var item = new Item
                    {
                        Id = Guid.NewGuid(),
                        Name = itemName,
                        Quantity = quantity,
                        ShoppingListId = listId,
                        CategoryId = categoryId,
                        SubCategoryId = subCategoryId,
                        IsChecked = false,
                        AddedAt = DateTimeOffset.UtcNow,
                    };

                    _context.Items.Add(item);
                    await _context.SaveChangesAsync();
                    existingItems.Add(item);

                    await _context.Entry(item).Reference(i => i.Category).LoadAsync();
                    await _context.Entry(item).Reference(i => i.SubCategory).LoadAsync();

                    dto = new ItemWithCategoryDto
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Quantity = item.Quantity,
                        IsChecked = item.IsChecked,
                        AddedAt = item.AddedAt,
                        ShoppingListId = item.ShoppingListId,
                        CategoryId = item.CategoryId,
                        CategoryName = item.Category?.Name,
                        SubCategoryId = item.SubCategoryId,
                        SubCategoryName = item.SubCategory?.Name,
                    };

                    await _hubContext.Clients
                        .Group($"list_{listId}")
                        .SendAsync("ReceiveItemAdded", dto);
                }

                addedItems.Add(dto);
            }

            return Ok(new { addedCount = addedItems.Count });
        }
    }
}
