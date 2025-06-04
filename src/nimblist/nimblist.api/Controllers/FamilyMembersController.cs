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
    public class FamilyMembersController : ControllerBase
    {
        private readonly NimblistContext _context;

        public FamilyMembersController(NimblistContext context)
        {
            _context = context;
        }

        // POST: api/FamilyMembers
        [HttpPost]
        public async Task<ActionResult<FamilyMember>> PostFamilyMember(FamilyMemberInputDto dto)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim not found.");

            var family = await _context.Families.FirstOrDefaultAsync(f => f.Id == dto.FamilyId);
            if (family == null) return NotFound("Family not found.");

            // Check if current user owns the family
            if (family.UserId != currentUserId) //
            {
                return Forbid("You do not have permission to add members to this family.");
            }

            // Check if user to add exists
            var userToAdd = await _context.Users.FindAsync(dto.UserIdToAdd);
            if (userToAdd == null) return BadRequest("User to add not found.");

            // Check if already a member
            var alreadyMember = await _context.FamilyMembers
                                      .AnyAsync(fm => fm.FamilyId == dto.FamilyId && fm.UserId == dto.UserIdToAdd);
            if (alreadyMember) return Conflict("User is already a member of this family.");

            var familyMember = new FamilyMember
            {
                FamilyId = dto.FamilyId,
                UserId = dto.UserIdToAdd //
            };

            _context.FamilyMembers.Add(familyMember);
            await _context.SaveChangesAsync();

            // Load navigation properties for the response
            await _context.Entry(familyMember).Reference(fm => fm.User).LoadAsync();
            await _context.Entry(familyMember).Reference(fm => fm.Family).LoadAsync();

            return CreatedAtAction(nameof(GetFamilyMember), new { id = familyMember.Id }, familyMember);
        }

        // DELETE: api/FamilyMembers/{id} (where 'id' is FamilyMember.Id)
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFamilyMember(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim not found.");

            var familyMember = await _context.FamilyMembers
                                       .Include(fm => fm.Family) //
                                       .FirstOrDefaultAsync(fm => fm.Id == id);

            if (familyMember == null) return NotFound("Family member record not found.");

            // Allow removal if:
            // 1. Current user is the owner of the family.
            // 2. Current user is the member being removed (self-removal).
            if (familyMember.Family.UserId != currentUserId && familyMember.UserId != currentUserId)
            {
                return Forbid("You do not have permission to remove this family member.");
            }

            // Edge case: Prevent family owner from removing their own FamilyMember record if it's critical
            // For now, this logic allows owner to remove their own membership link.
            // The family itself still exists and has an owner (Family.UserId).

            _context.FamilyMembers.Remove(familyMember);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/FamilyMembers/{id} (where 'id' is FamilyMember.Id)
        [HttpGet("{id}")]
        public async Task<ActionResult<FamilyMember>> GetFamilyMember(Guid id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim not found.");

            var familyMember = await _context.FamilyMembers
                                        .Include(fm => fm.User) // Consider creating a User DTO to avoid over-exposing ApplicationUser details
                                        .Include(fm => fm.Family)
                                        .FirstOrDefaultAsync(fm => fm.Id == id);

            if (familyMember == null) return NotFound("Family member record not found.");

            // Allow view if:
            // 1. Current user is the owner of the family.
            // 2. Current user is the member themselves.
            // 3. Current user is any member of the same family (to see other members).
            bool isOwner = familyMember.Family.UserId == currentUserId;
            bool isSelf = familyMember.UserId == currentUserId;
            bool isMemberOfSameFamily = await _context.FamilyMembers
                                             .AnyAsync(fm => fm.FamilyId == familyMember.FamilyId && fm.UserId == currentUserId);

            if (!isOwner && !isSelf && !isMemberOfSameFamily)
            {
                return Forbid("You do not have permission to view this family membership record.");
            }

            return Ok(familyMember);
        }

        // GET: api/FamilyMembers?familyId={familyId}
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FamilyMember>>> GetFamilyMembersForFamily([FromQuery] Guid familyId)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("User ID claim not found.");

            var family = await _context.Families.FindAsync(familyId);
            if (family == null) return NotFound("Family not found.");

            // Allow view if current user is owner or a member of the specified family
            bool isOwner = family.UserId == currentUserId;
            bool isMember = await _context.FamilyMembers.AnyAsync(fm => fm.FamilyId == familyId && fm.UserId == currentUserId);

            if (!isOwner && !isMember)
            {
                return Forbid("You do not have permission to view members of this family.");
            }

            var members = await _context.FamilyMembers
                                  .Where(fm => fm.FamilyId == familyId)
                                  .Include(fm => fm.User) // Again, consider User DTO
                                  .Include(fm => fm.Family) // Could be omitted if familyId is already known from query
                                  .ToListAsync();
            return Ok(members);
        }
    }
}