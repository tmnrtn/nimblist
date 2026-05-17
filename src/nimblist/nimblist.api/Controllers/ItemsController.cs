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

        private async Task<HashSet<Guid>> GetAccessibleListIdsAsync(string userId)
        {
            var ownedIds = await _context.ShoppingLists
                .Where(l => l.UserId == userId)
                .Select(l => l.Id)
                .ToListAsync();

            var userShareIds = await _context.ListShares
                .Where(s => s.UserId == userId)
                .Select(s => s.ListId)
                .ToListAsync();

            var familyIds = await _context.FamilyMembers
                .Where(m => m.UserId == userId)
                .Select(m => m.FamilyId)
                .ToListAsync();

            var familyShareIds = familyIds.Count > 0
                ? await _context.ListShares
                    .Where(s => s.FamilyId.HasValue && familyIds.Contains(s.FamilyId.Value))
                    .Select(s => s.ListId)
                    .ToListAsync()
                : new List<Guid>();

            var result = new HashSet<Guid>(ownedIds);
            result.UnionWith(userShareIds);
            result.UnionWith(familyShareIds);
            return result;
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

            var accessibleListIds = await GetAccessibleListIdsAsync(userId);

            var items = await _context.Items
                                        .Include(i => i.List)
                                        .Include(i => i.Category)
                                        .Include(i => i.SubCategory)
                                        .Where(i => accessibleListIds.Contains(i.ShoppingListId))
                                        .OrderByDescending(i => i.AddedAt)
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

            var accessibleListIds = await GetAccessibleListIdsAsync(userId);

            var item = await _context.Items
                .Include(i => i.List)
                .Include(i => i.Category)
                .Include(i => i.SubCategory)
                .FirstOrDefaultAsync(i => i.Id == id && accessibleListIds.Contains(i.ShoppingListId));

            if (item == null) return NotFound();

            return Ok(ConvertToItemDto(item));
        }

        // PUT: api/Items/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(Guid id, ItemUpdateDto itemDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var accessibleListIds = await GetAccessibleListIdsAsync(userId);

            var existingItem = await _context.Items
                .Include(i => i.List)
                .Include(i => i.Category)
                .Include(i => i.SubCategory)
                .FirstOrDefaultAsync(i => i.Id == id && accessibleListIds.Contains(i.ShoppingListId));

            if (existingItem == null) return NotFound();

            // Target list must also be accessible (guards against moving item to another user's list)
            if (!accessibleListIds.Contains(itemDto.ShoppingListId))
                return Forbid();

            existingItem.Name = itemDto.Name;
            existingItem.Quantity = itemDto.Quantity;
            existingItem.IsChecked = itemDto.IsChecked;
            existingItem.ShoppingListId = itemDto.ShoppingListId;
            existingItem.CategoryId = itemDto.CategoryId;
            existingItem.SubCategoryId = itemDto.SubCategoryId;

            try
            {
                await _context.SaveChangesAsync();

                await _context.Entry(existingItem).Reference(i => i.Category).LoadAsync();
                await _context.Entry(existingItem).Reference(i => i.SubCategory).LoadAsync();

                string groupName = $"list_{existingItem.ShoppingListId}";
                var itemWithCategory = ConvertToItemDto(existingItem);
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveItemUpdated", itemWithCategory);

            }
            catch (DbUpdateConcurrencyException) { return Conflict("Concurrency conflict."); }

            return NoContent();
        }

        // POST: api/Items
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<ItemWithCategoryDto>> PostItem(ItemInputDto itemDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var accessibleListIds = await GetAccessibleListIdsAsync(userId);
            if (!accessibleListIds.Contains(itemDto.ShoppingListId))
                return Forbid();

            // Check for an existing unchecked item with the same name on this list
            var duplicate = await _context.Items
                .Include(i => i.Category)
                .Include(i => i.SubCategory)
                .FirstOrDefaultAsync(i =>
                    i.ShoppingListId == itemDto.ShoppingListId &&
                    !i.IsChecked &&
                    i.Name.ToLower() == itemDto.Name.ToLower());

            if (duplicate != null)
            {
                // Merge quantities and return the updated item rather than creating a new row
                duplicate.Quantity = Services.QuantityHelper.Merge(duplicate.Quantity, itemDto.Quantity);
                await _context.SaveChangesAsync();

                var mergedDto = ConvertToItemDto(duplicate);
                string mergedGroup = $"list_{duplicate.ShoppingListId}";
                await _hubContext.Clients.Group(mergedGroup).SendAsync("ReceiveItemUpdated", mergedDto);
                Console.WriteLine($"--> SignalR: Sent ReceiveItemUpdated (merged duplicate) to {mergedGroup} for item {duplicate.Id}");

                return Ok(mergedDto);
            }

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

            var accessibleListIds = await GetAccessibleListIdsAsync(userId);

            var item = await _context.Items
                .FirstOrDefaultAsync(i => i.Id == id && accessibleListIds.Contains(i.ShoppingListId));

            if (item == null) return NotFound();

            _context.Items.Remove(item);
            await _context.SaveChangesAsync();

            string groupName = $"list_{item.ShoppingListId}";
            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveItemDeleted", item.Id);

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
