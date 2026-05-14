using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.Data;
using Nimblist.Data.Models;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ShoppingListsController : ControllerBase
    {
        private readonly NimblistContext _context;

        public ShoppingListsController(NimblistContext context)
        {
            _context = context;
        }

        private async Task<List<ShoppingList>> GetUserShoppingLists(string userId)
        {
            var userSharedLists = await _context.ShoppingLists
                .Join(_context.ListShares,
                    sl => sl.Id,
                    ls => ls.ListId,
                    (sl, ls) => new { sl, ls })
                .Where(sls => sls.ls.UserId == userId)
                .Select(sls => sls.sl)
                .Distinct()
                .Include(sl => sl.Items)
                    .ThenInclude(i => i.Category) // Include Category information
                .Include(sl => sl.Items)
                    .ThenInclude(i => i.SubCategory) // Include SubCategory information
                .Include(sl => sl.Items)
                    .ThenInclude(i => i.Recipe) // Include Recipe information
                .ToListAsync();

            var famiySharedLists = await _context.ShoppingLists
                .Join(_context.ListShares,
                    sl => sl.Id,
                    ls => ls.ListId,
                    (sl, ls) => new { sl, ls })
                .Join(_context.FamilyMembers,
                    sls => sls.ls.FamilyId,
                    fm => fm.FamilyId,
                    (sls, fm) => new { sls.sl, fm })
                .Where(slfm => slfm.fm.UserId == userId)
                .Select(slfm => slfm.sl)
                .Distinct()
                .Include(sl => sl.Items)
                    .ThenInclude(i => i.Category) // Include Category information
                .Include(sl => sl.Items)
                    .ThenInclude(i => i.SubCategory) // Include SubCategory information
                .Include(sl => sl.Items)
                    .ThenInclude(i => i.Recipe) // Include Recipe information
                .ToListAsync();

            return userSharedLists.Concat(famiySharedLists)
                .Distinct()
                .OrderByDescending(sl => sl.CreatedAt)
                .ToList();
        }

        // Helper method to convert regular Items to ItemWithCategoryDto
        private List<ItemWithCategoryDto> ConvertToItemDtos(ICollection<Item> items)
        {
            return items.Select(item => new ItemWithCategoryDto
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
                RecipeId = item.RecipeId,
                RecipeTitle = item.Recipe?.Title
            }).ToList();
        }

        // Helper method to convert ShoppingList to ShoppingListWithItemsDto
        private ShoppingListWithItemsDto ConvertToShoppingListDto(ShoppingList shoppingList)
        {
            return new ShoppingListWithItemsDto
            {
                Id = shoppingList.Id,
                Name = shoppingList.Name,
                UserId = shoppingList.UserId,
                CreatedAt = shoppingList.CreatedAt,
                IsTemplate = shoppingList.IsTemplate,
                Items = ConvertToItemDtos(shoppingList.Items)
            };
        }

        // GET: api/ShoppingLists
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ShoppingListWithItemsDto>>> GetShoppingLists()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var userShoppingLists = await GetUserShoppingLists(userId);

            // Transform the shopping lists to include the category names
            var result = userShoppingLists.Select(list => ConvertToShoppingListDto(list)).ToList();

            return Ok(result);
        }

        // GET: api/ShoppingLists/templates
        [HttpGet("templates")]
        public async Task<ActionResult<IEnumerable<ShoppingListWithItemsDto>>> GetTemplates()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var templates = await _context.ShoppingLists
                .Where(sl => sl.UserId == userId && sl.IsTemplate)
                .Include(sl => sl.Items).ThenInclude(i => i.Category)
                .Include(sl => sl.Items).ThenInclude(i => i.SubCategory)
                .Include(sl => sl.Items).ThenInclude(i => i.Recipe)
                .OrderByDescending(sl => sl.CreatedAt)
                .ToListAsync();

            return Ok(templates.Select(ConvertToShoppingListDto));
        }

        // POST: api/ShoppingLists/{id}/createfrom
        [HttpPost("{id}/createfrom")]
        public async Task<ActionResult<ShoppingListWithItemsDto>> CreateFromTemplate(Guid id, [FromBody] ShoppingListInputDto dto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var template = await _context.ShoppingLists
                .Include(sl => sl.Items)
                .FirstOrDefaultAsync(sl => sl.Id == id && sl.UserId == userId && sl.IsTemplate);

            if (template == null) return NotFound("Template not found.");

            var newList = new ShoppingList
            {
                Name = dto.Name,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                IsTemplate = false,
            };

            _context.ShoppingLists.Add(newList);
            await _context.SaveChangesAsync();

            // Copy items from template
            foreach (var templateItem in template.Items)
            {
                _context.Items.Add(new Item
                {
                    Name = templateItem.Name,
                    Quantity = templateItem.Quantity,
                    IsChecked = false,
                    ShoppingListId = newList.Id,
                    CategoryId = templateItem.CategoryId,
                    SubCategoryId = templateItem.SubCategoryId,
                    AddedAt = DateTimeOffset.UtcNow,
                });
            }
            await _context.SaveChangesAsync();

            // Add owner share
            _context.ListShares.Add(new ListShare { UserId = userId, ListId = newList.Id });
            await _context.SaveChangesAsync();

            // Reload with navigation properties for DTO
            var created = await _context.ShoppingLists
                .Include(sl => sl.Items).ThenInclude(i => i.Category)
                .Include(sl => sl.Items).ThenInclude(i => i.SubCategory)
                .Include(sl => sl.Items).ThenInclude(i => i.Recipe)
                .FirstAsync(sl => sl.Id == newList.Id);

            return CreatedAtAction(nameof(GetShoppingList), new { id = created.Id }, ConvertToShoppingListDto(created));
        }

        // GET: api/ShoppingLists/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ShoppingListWithItemsDto>> GetShoppingList(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var userShoppingLists = await GetUserShoppingLists(userId);

            // Find the list matching ID AND UserId
            var shoppingList = userShoppingLists
                                        .FirstOrDefault(sl => sl.Id == id); // <<< Filter by Id AND UserId

            if (shoppingList == null)
            {
                // Return NotFound - don't reveal if the list exists but belongs to someone else
                return NotFound();
            }

            // Transform the shopping list to include the category names
            var result = ConvertToShoppingListDto(shoppingList);

            return Ok(result);
        }

        // PUT: api/ShoppingLists/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutShoppingList(Guid id, ShoppingListUpdateDto listDto) // Accept DTO
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var userShoppingLists = await GetUserShoppingLists(userId);

            // Find the list matching ID AND UserId
            var existingList = userShoppingLists
                                        .FirstOrDefault(sl => sl.Id == id); // <<< Filter by Id AND UserId

            if (existingList == null)
            {
                return NotFound(); // Not found or doesn't belong to user
            }

            // Update only allowed properties from DTO
            existingList.Name = listDto.Name;
            existingList.IsTemplate = listDto.IsTemplate;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException) { return Conflict("Concurrency conflict."); } // Handle concurrency

            return NoContent(); // Success
        }

        // POST: api/ShoppingLists
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ShoppingListWithItemsDto>> PostShoppingList(ShoppingListInputDto listDto) // Accept DTO
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            // Create the entity based on the DTO and the current user ID
            var shoppingList = new ShoppingList
            {
                Name = listDto.Name,
                UserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                IsTemplate = listDto.IsTemplate,
            };
            // EF Core generates the Guid Id automatically

            _context.ShoppingLists.Add(shoppingList);
            await _context.SaveChangesAsync();

            var listShare = new ListShare
            {
                UserId = userId,
                ListId = shoppingList.Id
            };

            _context.ListShares.Add(listShare);
            await _context.SaveChangesAsync();

            // Convert to DTO before returning
            var shoppingListDto = new ShoppingListWithItemsDto
            {
                Id = shoppingList.Id,
                Name = shoppingList.Name,
                UserId = shoppingList.UserId,
                CreatedAt = shoppingList.CreatedAt,
                IsTemplate = shoppingList.IsTemplate,
                Items = new List<ItemWithCategoryDto>()
            };

            return CreatedAtAction(nameof(GetShoppingList), new { id = shoppingList.Id }, shoppingListDto);
        }

        // DELETE: api/ShoppingLists/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteShoppingList(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            // Find the list matching Id AND UserId to ensure ownership
            var userShoppingLists = await GetUserShoppingLists(userId);

            // Find the list matching ID AND UserId
            var shoppingList = userShoppingLists
                                        .FirstOrDefault(sl => sl.Id == id); // <<< Filter by Id AND UserId

            if (shoppingList == null)
            {
                return NotFound(); // Not found or doesn't belong to user
            }

            _context.ShoppingLists.Remove(shoppingList);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
