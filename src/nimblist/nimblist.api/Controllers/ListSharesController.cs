using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nimblist.api.DTO;
using Nimblist.Data;
using Nimblist.Data.Models;
using System.Security.Claims;

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

        private static ListShareDetailDto ToDto(ListShare ls) =>
            new(ls.Id, ls.ListId, ls.UserId, ls.User?.Email, ls.FamilyId, ls.Family?.Name, ls.SharedAt);

        [HttpPost]
        public async Task<ActionResult<ListShareDetailDto>> PostListShare(ListShareInputDto dto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            if (string.IsNullOrEmpty(dto.UserIdToShareWith) && !dto.FamilyIdToShareWith.HasValue)
                return BadRequest("Either UserIdToShareWith or FamilyIdToShareWith must be provided.");
            if (!string.IsNullOrEmpty(dto.UserIdToShareWith) && dto.FamilyIdToShareWith.HasValue)
                return BadRequest("Share with a user OR a family, not both.");

            var list = await _context.ShoppingLists.FirstOrDefaultAsync(sl => sl.Id == dto.ListId);
            if (list == null) return NotFound("Shopping list not found.");
            if (list.UserId != currentUserId) return Forbid();

            bool duplicate = !string.IsNullOrEmpty(dto.UserIdToShareWith)
                ? await _context.ListShares.AnyAsync(ls => ls.ListId == dto.ListId && ls.UserId == dto.UserIdToShareWith)
                : await _context.ListShares.AnyAsync(ls => ls.ListId == dto.ListId && ls.FamilyId == dto.FamilyIdToShareWith);

            if (duplicate) return Conflict("Already shared with that user or family.");

            var share = new ListShare { ListId = dto.ListId };
            var targetError = await ApplyShareTargetAsync(share, dto.UserIdToShareWith, dto.FamilyIdToShareWith, currentUserId);
            if (targetError != null) return targetError;

            _context.ListShares.Add(share);
            await _context.SaveChangesAsync();

            if (share.UserId != null) await _context.Entry(share).Reference(s => s.User).LoadAsync();
            if (share.FamilyId != null) await _context.Entry(share).Reference(s => s.Family).LoadAsync();

            return CreatedAtAction(nameof(GetListShare), new { id = share.Id }, ToDto(share));
        }

        private async Task<BadRequestObjectResult?> ApplyShareTargetAsync(
            ListShare share, string? userIdToShareWith, Guid? familyIdToShareWith, string currentUserId)
        {
            if (!string.IsNullOrEmpty(userIdToShareWith))
            {
                if (userIdToShareWith == currentUserId) return BadRequest("Cannot share with yourself.");
                if (await _context.Users.FindAsync(userIdToShareWith) == null) return BadRequest("User not found.");
                share.UserId = userIdToShareWith;
            }
            else
            {
                if (await _context.Families.FindAsync(familyIdToShareWith!.Value) == null) return BadRequest("Family not found.");
                share.FamilyId = familyIdToShareWith;
            }
            return null;
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteListShare(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var share = await _context.ListShares.Include(s => s.List).FirstOrDefaultAsync(s => s.Id == id);
            if (share == null) return NotFound();
            if (share.List!.UserId != currentUserId) return Forbid();

            _context.ListShares.Remove(share);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ListShareDetailDto>> GetListShare(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var share = await _context.ListShares
                .Include(s => s.List)
                .Include(s => s.User)
                .Include(s => s.Family)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (share == null) return NotFound();

            bool isOwner = share.List!.UserId == currentUserId;
            bool isDirectRecipient = share.UserId == currentUserId;
            bool isFamilyMember = share.FamilyId.HasValue &&
                await _context.FamilyMembers.AnyAsync(m => m.FamilyId == share.FamilyId && m.UserId == currentUserId);

            if (!isOwner && !isDirectRecipient && !isFamilyMember) return Forbid();
            return Ok(ToDto(share));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ListShareDetailDto>>> GetSharesForList([FromQuery] Guid listId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized();

            var list = await _context.ShoppingLists.FindAsync(listId);
            if (list == null) return NotFound();
            if (list.UserId != currentUserId) return Forbid();

            var shares = await _context.ListShares
                .Where(s => s.ListId == listId)
                .Include(s => s.User)
                .Include(s => s.Family)
                .ToListAsync();

            return Ok(shares.Select(ToDto));
        }
    }
}
