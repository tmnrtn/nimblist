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
    public class RecipesController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IClassificationService _classificationService;
        private readonly IHubContext<ShoppingListHub> _hubContext;
        private readonly ILogger<RecipesController> _logger;

        public RecipesController(
            NimblistContext context,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            IClassificationService classificationService,
            IHubContext<ShoppingListHub> hubContext,
            ILogger<RecipesController> logger)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _classificationService = classificationService;
            _hubContext = hubContext;
            _logger = logger;
        }

        public record ImportRecipeRequest(string Url);

        // POST /api/recipes/import
        [HttpPost("import")]
        public async Task<ActionResult<RecipeDetailDto>> ImportRecipe([FromBody] ImportRecipeRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Url))
                return BadRequest("URL is required.");

            var scraperUrl = _configuration["RecipeScraperService:ScrapeUrl"];
            if (string.IsNullOrEmpty(scraperUrl))
                return StatusCode(503, "Recipe scraper service is not configured.");

            ScraperResponseDto? scraped;
            try
            {
                var client = _httpClientFactory.CreateClient("RecipeScraperClient");
                var response = await client.PostAsJsonAsync(scraperUrl, new { url = request.Url });
                scraped = await response.Content.ReadFromJsonAsync<ScraperResponseDto>();

                if (!response.IsSuccessStatusCode || scraped?.Error != null)
                {
                    var errorMsg = scraped?.Error ?? $"Scraper returned {(int)response.StatusCode}";
                    return UnprocessableEntity(new { error = errorMsg });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to call recipe scraper for URL: {Url}", request.Url);
                return StatusCode(503, "Recipe scraper service is unavailable.");
            }

            if (scraped == null)
                return UnprocessableEntity(new { error = "No data returned from scraper." });

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = scraped.Title ?? "Untitled Recipe",
                Description = scraped.Description,
                SourceUrl = request.Url,
                ImageUrl = scraped.Image,
                Yields = scraped.Yields,
                TotalTimeMinutes = scraped.TotalTime,
                Instructions = scraped.Instructions,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            for (int i = 0; i < scraped.Ingredients.Count; i++)
            {
                var ing = scraped.Ingredients[i];
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    Text = ing.Text,
                    ParsedName = ing.ParsedName,
                    ParsedQuantity = ing.ParsedQuantity,
                    SortOrder = i,
                });
            }

            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, ToDetailDto(recipe, userId!));
        }

        // POST /api/recipes
        [HttpPost]
        public async Task<ActionResult<RecipeDetailDto>> CreateRecipe([FromBody] CreateRecipeRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Title is required.");

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = request.Title.Trim(),
                Description = request.Description,
                SourceUrl = request.SourceUrl,
                ImageUrl = request.ImageUrl,
                Yields = request.Yields,
                TotalTimeMinutes = request.TotalTimeMinutes,
                Instructions = request.Instructions,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
            };

            for (int i = 0; i < request.Ingredients.Count; i++)
            {
                var ing = request.Ingredients[i];
                recipe.Ingredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    Text = ing.Text,
                    ParsedName = ing.ParsedName,
                    ParsedQuantity = ing.ParsedQuantity,
                    SortOrder = i,
                });
            }

            _context.Recipes.Add(recipe);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRecipe), new { id = recipe.Id }, ToDetailDto(recipe, userId));
        }

        private async Task<HashSet<Guid>> GetAccessibleRecipeIdsAsync(string userId)
        {
            var ownIds = await _context.Recipes.Where(r => r.UserId == userId).Select(r => r.Id).ToListAsync();
            var userSharedIds = await _context.RecipeShares.Where(rs => rs.UserId == userId).Select(rs => rs.RecipeId).ToListAsync();
            var familyIds = await _context.FamilyMembers.Where(m => m.UserId == userId).Select(m => m.FamilyId).ToListAsync();
            var familySharedIds = familyIds.Count > 0
                ? await _context.RecipeShares.Where(rs => rs.FamilyId.HasValue && familyIds.Contains(rs.FamilyId.Value)).Select(rs => rs.RecipeId).ToListAsync()
                : new List<Guid>();

            return ownIds.Concat(userSharedIds).Concat(familySharedIds).ToHashSet();
        }

        // GET /api/recipes
        [HttpGet]
        public async Task<ActionResult<List<RecipeSummaryDto>>> GetRecipes()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var accessibleIds = await GetAccessibleRecipeIdsAsync(userId);

            var recipes = await _context.Recipes
                .Where(r => accessibleIds.Contains(r.Id))
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RecipeSummaryDto(
                    r.Id, r.Title, r.ImageUrl, r.Yields, r.TotalTimeMinutes,
                    r.Ingredients.Count, r.CreatedAt, r.UserId == userId))
                .ToListAsync();

            return Ok(recipes);
        }

        // GET /api/recipes/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<RecipeDetailDto>> GetRecipe(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var accessibleIds = await GetAccessibleRecipeIdsAsync(userId);
            if (!accessibleIds.Contains(id)) return NotFound();

            var recipe = await _context.Recipes
                .Include(r => r.Ingredients.OrderBy(i => i.SortOrder))
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null) return NotFound();
            return Ok(ToDetailDto(recipe, userId!));
        }

        // PUT /api/recipes/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<RecipeDetailDto>> UpdateRecipe(Guid id, [FromBody] CreateRecipeRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Title is required.");

            var recipe = await _context.Recipes
                .Include(r => r.Ingredients)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (recipe == null) return NotFound();

            recipe.Title = request.Title.Trim();
            recipe.Description = request.Description;
            recipe.SourceUrl = request.SourceUrl;
            recipe.ImageUrl = request.ImageUrl;
            recipe.Yields = request.Yields;
            recipe.TotalTimeMinutes = request.TotalTimeMinutes;
            recipe.Instructions = request.Instructions;

            _context.RecipeIngredients.RemoveRange(recipe.Ingredients);
            recipe.Ingredients.Clear();

            // Re-parse any ingredients whose text changed (frontend sends null parsed fields for those)
            var reparsed = await ParseIngredientsAsync(request.Ingredients);

            for (int i = 0; i < request.Ingredients.Count; i++)
            {
                var ing = request.Ingredients[i];
                _context.RecipeIngredients.Add(new RecipeIngredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Text = ing.Text,
                    ParsedName = reparsed[i].ParsedName,
                    ParsedQuantity = reparsed[i].ParsedQuantity,
                    SortOrder = i,
                });
            }

            await _context.SaveChangesAsync();
            return Ok(ToDetailDto(recipe, userId));
        }

        // DELETE /api/recipes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRecipe(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Only the owner can delete
            var recipe = await _context.Recipes.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);
            if (recipe == null) return NotFound();

            _context.Recipes.Remove(recipe);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // POST /api/recipes/{id}/addtolist/{listId}
        [HttpPost("{id}/addtolist/{listId}")]
        public async Task<ActionResult> AddIngredientsToList(Guid id, Guid listId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            // Accessible to owner and anyone the recipe was shared with
            var accessibleIds = await GetAccessibleRecipeIdsAsync(userId);
            if (!accessibleIds.Contains(id)) return NotFound("Recipe not found.");

            var recipe = await _context.Recipes
                .Include(r => r.Ingredients.OrderBy(i => i.SortOrder))
                .FirstOrDefaultAsync(r => r.Id == id);

            if (recipe == null) return NotFound();

            var listExists = await _context.ShoppingLists
                .AnyAsync(sl => sl.Id == listId && sl.UserId == userId);

            if (!listExists) return NotFound("Shopping list not found.");

            var addedItems = new List<ItemWithCategoryDto>();

            foreach (var ingredient in recipe.Ingredients)
            {
                var itemName = ingredient.ParsedName ?? ingredient.Text;
                var (categoryId, subCategoryId) = await _classificationService.ClassifyAsync(itemName);

                var item = new Item
                {
                    Id = Guid.NewGuid(),
                    Name = itemName,
                    Quantity = ingredient.ParsedQuantity,
                    ShoppingListId = listId,
                    CategoryId = categoryId,
                    SubCategoryId = subCategoryId,
                    IsChecked = false,
                    AddedAt = DateTimeOffset.UtcNow,
                };

                _context.Items.Add(item);
                await _context.SaveChangesAsync();

                await _context.Entry(item).Reference(i => i.Category).LoadAsync();
                await _context.Entry(item).Reference(i => i.SubCategory).LoadAsync();

                var dto = new ItemWithCategoryDto
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

                addedItems.Add(dto);

                await _hubContext.Clients
                    .Group($"list_{listId}")
                    .SendAsync("ReceiveItemAdded", dto);
            }

            return Ok(new { addedCount = addedItems.Count });
        }

        private async Task<List<ScraperIngredientDto>> ParseIngredientsAsync(List<RecipeIngredientInputDto> ingredients)
        {
            // Ingredients that already have parsed fields (text unchanged) are passed through.
            // Only those with null parsed fields are sent to the parser service.
            var needsParsing = ingredients
                .Select((ing, i) => (ing, i))
                .Where(x => x.ing.ParsedName == null)
                .ToList();

            var result = ingredients
                .Select(ing => new ScraperIngredientDto { Text = ing.Text, ParsedName = ing.ParsedName, ParsedQuantity = ing.ParsedQuantity })
                .ToList();

            if (needsParsing.Count == 0) return result;

            var parseUrl = _configuration["RecipeScraperService:ParseUrl"];
            if (string.IsNullOrEmpty(parseUrl)) return result;

            try
            {
                var client = _httpClientFactory.CreateClient("RecipeScraperClient");
                var response = await client.PostAsJsonAsync(parseUrl, new { ingredients = needsParsing.Select(x => x.ing.Text).ToList() });
                if (!response.IsSuccessStatusCode) return result;

                var parsed = await response.Content.ReadFromJsonAsync<List<ScraperIngredientDto>>();
                if (parsed == null || parsed.Count != needsParsing.Count) return result;

                for (int i = 0; i < needsParsing.Count; i++)
                {
                    result[needsParsing[i].i].ParsedName = parsed[i].ParsedName;
                    result[needsParsing[i].i].ParsedQuantity = parsed[i].ParsedQuantity;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse ingredients; saving with null parsed fields.");
            }

            return result;
        }

        private static RecipeDetailDto ToDetailDto(Recipe recipe, string userId) =>
            new(
                recipe.Id,
                recipe.Title,
                recipe.Description,
                recipe.SourceUrl,
                recipe.ImageUrl,
                recipe.Yields,
                recipe.TotalTimeMinutes,
                recipe.Instructions,
                recipe.CreatedAt,
                recipe.Ingredients
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new RecipeIngredientDto(i.Id, i.Text, i.ParsedName, i.ParsedQuantity, i.SortOrder))
                    .ToList(),
                recipe.UserId == userId
            );
    }
}
