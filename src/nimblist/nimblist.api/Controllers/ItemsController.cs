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
    public class ItemsController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly IHubContext<ShoppingListHub> _hubContext;
        private readonly IClassificationService _classificationService;
        private readonly IPushNotificationService _pushNotificationService;

        public ItemsController(
            NimblistContext context,
            IHubContext<ShoppingListHub> hubContext,
            IClassificationService classificationService,
            IPushNotificationService pushNotificationService)
        {
            _context = context;
            _hubContext = hubContext;
            _classificationService = classificationService;
            _pushNotificationService = pushNotificationService;
        }

        // Helper method to convert Item to ItemWithCategoryDto
        private ItemWithCategoryDto ConvertToItemDto(Item item)
        {
            return new ItemWithCategoryDto
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
                List = item.List
            };
        }

        // GET: api/Items
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ItemWithCategoryDto>>> GetItems()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var items = await _context.Items
                                        .Include(i => i.List) // Include the parent list
                                        .Include(i => i.Category) // Include Category information
                                        .Include(i => i.SubCategory) // Include SubCategory information
                                        .Where(i => i.List != null && i.List.UserId == userId) // Defensive: check List is not null
                                        .OrderByDescending(i => i.AddedAt) // Order by AddedAt
                                        .ToListAsync();

            var itemDtos = items.Select(item => ConvertToItemDto(item)).ToList();
            return Ok(itemDtos);
        }

        // GET: api/Items/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ItemWithCategoryDto>> GetItem(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var item = await _context.Items
                .Include(i => i.List) // Include the parent list
                .Include(i => i.Category) // Include Category information
                .Include(i => i.SubCategory) // Include SubCategory information
                .FirstOrDefaultAsync(i => i.Id == id && i.List != null && i.List.UserId == userId); // Defensive: check List is not null

            if (item == null)
            {
                return NotFound();
            }

            var itemDto = ConvertToItemDto(item);
            return Ok(itemDto);
        }

        // PUT: api/Items/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(Guid id, ItemUpdateDto itemDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var existingItem = await _context.Items
                .Include(i => i.List) // Include the parent list
                .Include(i => i.Category) // Include Category information
                .Include(i => i.SubCategory) // Include SubCategory information
                .FirstOrDefaultAsync(i => i.Id == id && i.List != null && i.List.UserId == userId); // Defensive: check List is not null

            if (existingItem == null)
            {
                return NotFound();
            }
            existingItem.Name = itemDto.Name;
            existingItem.Quantity = itemDto.Quantity;
            existingItem.IsChecked = itemDto.IsChecked;
            existingItem.ShoppingListId = itemDto.ShoppingListId;
            // Set category and subcategory if provided
            existingItem.CategoryId = itemDto.CategoryId;
            existingItem.SubCategoryId = itemDto.SubCategoryId;

            try
            {
                // Check if the shopping list exists
                var shoppingListExists = await _context.ShoppingLists.AnyAsync(sl => sl.Id == itemDto.ShoppingListId && sl.UserId == userId);
                if (!shoppingListExists)
                {
                    throw new InvalidOperationException("The required data for completing this operation was not found. The shopping list does not exist.");
                }
                
                await _context.SaveChangesAsync();

                // Reload navigation properties to ensure names are up-to-date
                await _context.Entry(existingItem).Reference(i => i.Category).LoadAsync();
                await _context.Entry(existingItem).Reference(i => i.SubCategory).LoadAsync();

                // --- Send SignalR Update ---
                string groupName = $"list_{existingItem.ShoppingListId}";
                // Message name "ReceiveItemUpdated" must match client listener
                // Convert to DTO with category information
                var itemWithCategory = ConvertToItemDto(existingItem);
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveItemUpdated", itemWithCategory);
                Console.WriteLine($"--> SignalR: Sent ReceiveItemUpdated to {groupName} for item {existingItem.Id}");
                // --------------------------
            }
            catch (DbUpdateConcurrencyException) { return Conflict("Concurrency conflict."); } // Handle concurrency

            return NoContent(); // Success
        }

        // POST: api/Items
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ItemWithCategoryDto>> PostItem(ItemInputDto itemDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var (foundCategoryId, foundSubCategoryId) = await _classificationService.ClassifyAsync(itemDto.Name);

            var item = new Item
            {
                Name = itemDto.Name,
                Quantity = itemDto.Quantity,
                IsChecked = itemDto.IsChecked,
                ShoppingListId = itemDto.ShoppingListId,
                CategoryId = foundCategoryId,
                SubCategoryId = foundSubCategoryId
            };

            _context.Items.Add(item);
            await _context.SaveChangesAsync();

            // Reload the item with category information
            await _context.Entry(item).Reference(i => i.Category).LoadAsync();
            await _context.Entry(item).Reference(i => i.SubCategory).LoadAsync();

            // --- Send SignalR Update ---
            string groupName = $"list_{item.ShoppingListId}";
            // Message name "ReceiveItemAdded" must match client listener
            // Convert to DTO with category information
            var itemWithCategory = ConvertToItemDto(item);
            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveItemAdded", itemWithCategory);
            Console.WriteLine($"--> SignalR: Sent ReceiveItemAdded to {groupName} for item {item.Id}");
            // --------------------------

            _ = Task.Run(() => _pushNotificationService.NotifyItemAddedAsync(item, userId));

            // Record or update the previous item name usage
            var prevName = await _context.PreviousItemNames.FirstOrDefaultAsync(p => p.UserId == userId && p.Name == itemDto.Name);
            if (prevName == null)
            {
                _context.PreviousItemNames.Add(new PreviousItemName { Name = itemDto.Name, UserId = userId, LastUsed = DateTimeOffset.UtcNow });
            }
            else
            {
                prevName.LastUsed = DateTimeOffset.UtcNow;
            }
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetItem), new { id = item.Id }, itemWithCategory);
        }

        // DELETE: api/Items/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var item = await _context.Items
                .Include(i => i.List) // Optionally include the parent list
                .FirstOrDefaultAsync(i => i.Id == id && i.List != null && i.List.UserId == userId); // Defensive: check List is not null
            if (item == null)
            {
                return NotFound();
            }

            _context.Items.Remove(item);
            await _context.SaveChangesAsync();

            // --- Send SignalR Update ---
            string groupName = $"list_{item.ShoppingListId}";
            // Message name "ReceiveItemDeleted" must match client listener
            // Send just the ID of the item that was deleted
            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveItemDeleted", item.Id);
            Console.WriteLine($"--> SignalR: Sent ReceiveItemDeleted to {groupName} for item {item.Id}");
            // --------------------------

            return NoContent();
        }

        // GET: api/Items/previous-names
        [HttpGet("previous-names")]
        public async Task<ActionResult<IEnumerable<string>>> GetPreviousItemNames()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");
            var names = await _context.PreviousItemNames
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.LastUsed)
                .Select(p => p.Name)
                .ToListAsync();
            return Ok(names);
        }
    }
}
