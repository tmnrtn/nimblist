using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.api.Hubs;
using Nimblist.Data;
using Nimblist.Data.Models;
using StackExchange.Redis;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ItemsController : ControllerBase
    {
        private readonly NimblistContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHubContext<ShoppingListHub> _hubContext;

        public ItemsController(
            NimblistContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<ShoppingListHub> hubContext) // << Inject Hub Context here
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext; // << Store the injected context
        }

        // GET: api/Items
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Item>>> GetItems()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var items = await _context.Items
                                        .Include(i => i.List) // Optionally include the parent list
                                        .Where(i => i.List.UserId == userId) // <<< Filter by UserId
                                        .OrderByDescending(i => i.AddedAt) // Optional: Order them
                                        .ToListAsync();

            return Ok(items);
        }

        // GET: api/Items/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Item>> GetItem(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var item = await _context.Items
                .Include(i => i.List) // Optionally include the parent list
                .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

            if (item == null)
            {
                return NotFound();
            }

            return Ok(item);
        }

        // PUT: api/Items/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutItem(Guid id, ItemUpdateDto itemDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var existingItem = await _context.Items
                .Include(i => i.List) // Optionally include the parent list
                .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

            if (existingItem == null)
            {
                return NotFound();
            }

            existingItem.Name = itemDto.Name;
            existingItem.Quantity = itemDto.Quantity;
            existingItem.IsChecked = itemDto.IsChecked;
            existingItem.ShoppingListId = itemDto.ShoppingListId;

            try
            {
                await _context.SaveChangesAsync();

                // --- Send SignalR Update ---
                string groupName = $"list_{existingItem.ShoppingListId}";
                // Message name "ReceiveItemUpdated" must match client listener
                // Send the updated item data back (the version from DB after save)
                await _hubContext.Clients.Group(groupName).SendAsync("ReceiveItemUpdated", existingItem);
                Console.WriteLine($"--> SignalR: Sent ReceiveItemUpdated to {groupName} for item {existingItem.Id}");
                // --------------------------
            }
            catch (DbUpdateConcurrencyException) { return Conflict("Concurrency conflict."); } // Handle concurrency

            return NoContent(); // Success
        }

        // POST: api/Items
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Item>> PostItem(ItemInputDto itemDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var item = new Item
            {
                Name = itemDto.Name,
                Quantity = itemDto.Quantity,
                IsChecked = itemDto.IsChecked,
                ShoppingListId = itemDto.ShoppingListId
            };

            _context.Items.Add(item);
            await _context.SaveChangesAsync();

            // --- Send SignalR Update ---
            string groupName = $"list_{item.ShoppingListId}";
            // Message name "ReceiveItemAdded" must match client listener
            // Send the newly created item object as payload
            await _hubContext.Clients.Group(groupName).SendAsync("ReceiveItemAdded", item);
            Console.WriteLine($"--> SignalR: Sent ReceiveItemAdded to {groupName} for item {item.Id}");
            // --------------------------

            return CreatedAtAction(nameof(GetItem), new { id = item.Id }, item);
        }

        // DELETE: api/Items/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteItem(Guid id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var item = await _context.Items
                .Include(i => i.List) // Optionally include the parent list
                .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);
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

    }
}
