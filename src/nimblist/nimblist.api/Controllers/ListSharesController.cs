using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.Data;
using Nimblist.Data.Models;
using Nimblist.api.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace Nimblist.api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ListSharesController : ControllerBase
    {
        private readonly NimblistContext _context;

        public ListSharesController(NimblistContext context)
        {
            _context = context;
        }

        // POST: api/ListShares
        [HttpPost]
        public async Task<ActionResult<ListShare>> PostListShare(ListShareInputDto dto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim not found.");

            if (string.IsNullOrEmpty(dto.UserIdToShareWith) && !dto.FamilyIdToShareWith.HasValue)
            {
                return BadRequest("Either UserIdToShareWith or FamilyIdToShareWith must be provided.");
            }
            if (!string.IsNullOrEmpty(dto.UserIdToShareWith) && dto.FamilyIdToShareWith.HasValue)
            {
                return BadRequest("Cannot provide both UserIdToShareWith and FamilyIdToShareWith. Share with a user OR a family.");
            }

            var shoppingList = await _context.ShoppingLists.FirstOrDefaultAsync(sl => sl.Id == dto.ListId);
            if (shoppingList == null) return NotFound("Shopping list not found.");

            // Check if current user owns the shopping list
            if (shoppingList.UserId != currentUserId) //
            {
                return Forbid("You do not have permission to share this shopping list.");
            }

            // Prevent sharing list with its owner directly
            if (!string.IsNullOrEmpty(dto.UserIdToShareWith) && dto.UserIdToShareWith == currentUserId)
            {
                return BadRequest("Cannot share a list with its owner directly; owner already has access.");
            }

            // Check for duplicate share
            bool shareAlreadyExists = false;
            if (!string.IsNullOrEmpty(dto.UserIdToShareWith))
            {
                shareAlreadyExists = await _context.ListShares.AnyAsync(ls => ls.ListId == dto.ListId && ls.UserId == dto.UserIdToShareWith);
            }
            else if (dto.FamilyIdToShareWith.HasValue)
            {
                shareAlreadyExists = await _context.ListShares.AnyAsync(ls => ls.ListId == dto.ListId && ls.FamilyId == dto.FamilyIdToShareWith.Value);
            }
            if (shareAlreadyExists) return Conflict("This list is already shared with the specified user or family.");


            var listShare = new ListShare { ListId = dto.ListId }; //

            if (!string.IsNullOrEmpty(dto.UserIdToShareWith))
            {
                var userToShareWith = await _context.Users.FindAsync(dto.UserIdToShareWith);
                if (userToShareWith == null) return BadRequest("User to share with not found.");
                listShare.UserId = dto.UserIdToShareWith;
            }
            else if (dto.FamilyIdToShareWith.HasValue)
            {
                var familyToShareWith = await _context.Families.FindAsync(dto.FamilyIdToShareWith.Value);
                if (familyToShareWith == null) return BadRequest("Family to share with not found.");
                listShare.FamilyId = dto.FamilyIdToShareWith.Value;
            }

            _context.ListShares.Add(listShare);
            await _context.SaveChangesAsync();

            // Load navigation properties for the response
            await _context.Entry(listShare).Reference(ls => ls.List).LoadAsync();
            if (listShare.UserId != null) await _context.Entry(listShare).Reference(ls => ls.User).LoadAsync();
            if (listShare.FamilyId != null) await _context.Entry(listShare).Reference(ls => ls.Family).LoadAsync();


            return CreatedAtAction(nameof(GetListShare), new { id = listShare.Id }, listShare);
        }

        // DELETE: api/ListShares/{id} (where 'id' is ListShare.Id)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteListShare(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim not found.");

            var listShare = await _context.ListShares
                                    .Include(ls => ls.List) //
                                    .FirstOrDefaultAsync(ls => ls.Id == id);

            if (listShare == null) return NotFound("List share record not found.");

            // Allow removal if current user is the owner of the shopping list
            if (listShare.List.UserId != currentUserId) //
            {
                return Forbid("You do not have permission to remove this share.");
            }

            // Check if this is the owner's self-share created at list creation.
            // The ShoppingListsController creates an initial share for the owner.
            // Deleting this specific share might be undesirable if it's the primary access record for the owner.
            // However, ownership is primarily by ShoppingList.UserId.
            // For now, allow deletion of any share record if the user is the list owner.

            _context.ListShares.Remove(listShare);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/ListShares/{id} (where 'id' is ListShare.Id)
        [HttpGet("{id}")]
        public async Task<ActionResult<ListShare>> GetListShare(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim not found.");

            var listShare = await _context.ListShares
                                    .Include(ls => ls.List)
                                    .Include(ls => ls.User) // Consider DTO for User
                                    .Include(ls => ls.Family) // Consider DTO for Family
                                    .FirstOrDefaultAsync(ls => ls.Id == id);

            if (listShare == null) return NotFound("List share record not found.");

            // Allow view if:
            // 1. Current user is the owner of the list.
            // 2. Current user is the user it's shared with directly.
            // 3. Current user is a member of the family it's shared with.
            bool isOwner = listShare.List.UserId == currentUserId; //
            bool isSharedWithUserDirectly = listShare.UserId == currentUserId;
            bool isMemberOfSharedFamily = false;
            if (listShare.FamilyId.HasValue)
            {
                isMemberOfSharedFamily = await _context.FamilyMembers
                                               .AnyAsync(fm => fm.FamilyId == listShare.FamilyId.Value && fm.UserId == currentUserId);
            }

            if (!isOwner && !isSharedWithUserDirectly && !isMemberOfSharedFamily)
            {
                return Forbid("You do not have permission to view this list share record.");
            }

            return Ok(listShare);
        }

        // GET: api/ListShares?listId={listId}
        // Gets all shares for a particular shopping list.
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ListShare>>> GetSharesForList([FromQuery] Guid listId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim not found.");

            var list = await _context.ShoppingLists.FindAsync(listId);
            if (list == null) return NotFound("Shopping list not found.");

            // Only the owner of the list should be able to see all its shares.
            if (list.UserId != currentUserId) //
            {
                return Forbid("You do not have permission to view all shares for this list.");
            }

            var shares = await _context.ListShares
                                  .Where(ls => ls.ListId == listId)
                                  .Include(ls => ls.User) // Consider DTO for User
                                  .Include(ls => ls.Family) // Consider DTO for Family
                                  .ToListAsync();
            return Ok(shares);
        }
    }
}