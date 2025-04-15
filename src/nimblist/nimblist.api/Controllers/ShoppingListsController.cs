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
                .Include(sl => sl.Items) // Include items if needed
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
                .Include(sl => sl.Items) // Include items if needed
                .ToListAsync();

            return userSharedLists.Concat(famiySharedLists)
                .Distinct()
                .OrderByDescending(sl => sl.CreatedAt)
                .ToList();
        }

        // GET: api/ShoppingLists
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ShoppingList>>> GetShoppingLists()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return Unauthorized("User ID claim not found.");

            var userShoppingLists = await GetUserShoppingLists(userId);

            return Ok(userShoppingLists);
        }

        // GET: api/ShoppingLists/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ShoppingList>> GetShoppingList(Guid id)
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

            return Ok(shoppingList);
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
        public async Task<ActionResult<ShoppingList>> PostShoppingList(ShoppingListInputDto listDto) // Accept DTO
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

            // Important: Return the fully created entity, not the DTO
            return CreatedAtAction(nameof(GetShoppingList), new { id = shoppingList.Id }, shoppingList);
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
