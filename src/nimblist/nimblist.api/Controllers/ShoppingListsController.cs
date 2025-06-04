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
                SubCategoryName = item.SubCategory?.Name
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
                UserId = userId, // <<< Assign the current user's ID
                CreatedAt = DateTimeOffset.UtcNow // Set server-side
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
                Items = new List<ItemWithCategoryDto>() // Empty list for a new shopping list
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
