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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;

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
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;  
        private readonly ILogger<ItemsController> _logger; 

        public ItemsController(
            NimblistContext context,
            UserManager<ApplicationUser> userManager,
            IHubContext<ShoppingListHub> hubContext,
            IHttpClientFactory httpClientFactory,  
            IConfiguration configuration,   
            ILogger<ItemsController> logger)   
        {
            _context = context;
            _userManager = userManager;
            _hubContext = hubContext;
            _httpClientFactory = httpClientFactory;   
            _configuration = configuration;    
            _logger = logger; 
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
                SubCategoryName = item.SubCategory?.Name
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
                                        .Where(i => i.List.UserId == userId) // Filter by UserId
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
                .FirstOrDefaultAsync(i => i.Id == id && i.List.UserId == userId);

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
            Guid? foundCategoryId = null;
            Guid? foundSubCategoryId = null;

            string classificationServiceUrl = _configuration["ClassificationService:PredictUrl"];

            if (!string.IsNullOrEmpty(classificationServiceUrl) && !string.IsNullOrWhiteSpace(itemDto.Name))
            {
                _logger.LogInformation("Attempting to classify item: {ItemName}", itemDto.Name);
                try
                {
                    var httpClient = _httpClientFactory.CreateClient("ClassificationServiceClient"); // Named client (optional)
                    var classificationRequest = new { product_name = itemDto.Name };

                    var response = await httpClient.PostAsJsonAsync(classificationServiceUrl, classificationRequest);

                    if (response.IsSuccessStatusCode)
                    {
                        var classificationResult = await response.Content.ReadFromJsonAsync<ClassificationResponseDto>();
                        if (classificationResult != null)
                        {
                            _logger.LogInformation("Classification result for '{ItemName}': Primary='{Primary}', Sub='{Sub}'",
                                itemDto.Name, classificationResult.PredictedPrimaryCategory, classificationResult.PredictedSubCategory);

                            if (!string.IsNullOrEmpty(classificationResult.PredictedPrimaryCategory) && classificationResult.PredictedPrimaryCategory != "Unknown")
                            {
                                var categoryName = classificationResult.PredictedPrimaryCategory;
                                // Using ToLowerInvariant for case-insensitive comparison
                                var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.ToLower());
                                if (category != null)
                                {
                                    foundCategoryId = category.Id;
                                    _logger.LogInformation("Found CategoryId: {CategoryId} for '{CategoryName}'", foundCategoryId, categoryName);

                                    if (!string.IsNullOrEmpty(classificationResult.PredictedSubCategory) && classificationResult.PredictedSubCategory != "Unknown" && classificationResult.PredictedSubCategory != "N/A" && classificationResult.PredictedSubCategory != "No Sub-Model")
                                    {
                                        var subCategoryName = classificationResult.PredictedSubCategory;
                                        var subCategory = await _context.SubCategories
                                            .FirstOrDefaultAsync(sc => sc.Name.ToLower() == subCategoryName.ToLower() && sc.ParentCategoryId == foundCategoryId);

                                        if (subCategory != null)
                                        {
                                            foundSubCategoryId = subCategory.Id;
                                            _logger.LogInformation("Found SubCategoryId: {SubCategoryId} for '{SubCategoryName}' under CategoryId {CategoryId}", foundSubCategoryId, subCategoryName, foundCategoryId);
                                        }
                                        else
                                        {
                                            _logger.LogWarning("SubCategory '{SubCategoryName}' not found in database for CategoryId {CategoryId}.", subCategoryName, foundCategoryId);
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning("Category '{CategoryName}' not found in database.", categoryName);
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Failed to deserialize classification response for item: {ItemName}", itemDto.Name);
                        }
                    }
                    else
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Error calling classification service for item '{ItemName}'. Status: {StatusCode}. Response: {ErrorContent}", itemDto.Name, response.StatusCode, errorContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception occurred while calling classification service for item: {ItemName}", itemDto.Name);
                }
            }
            else if (string.IsNullOrWhiteSpace(itemDto.Name))
            {
                _logger.LogWarning("Item name is empty, skipping classification.");
            }

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
